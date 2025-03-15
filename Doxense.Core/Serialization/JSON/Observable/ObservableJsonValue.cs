#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System.ComponentModel;

	/// <summary>Observable JSON Object that will capture all reads</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class ObservableJsonValue : IJsonProxyNode, IJsonSerializable, IJsonPackable
	{

		public ObservableJsonValue(IObservableJsonContext? ctx, IJsonProxyNode? parent, JsonPathSegment segment, JsonValue json)
		{
			Contract.Debug.Requires(json != null);
			this.Context = ctx;
			this.Parent = parent;
			this.Segment = segment;
			this.Json = json;
			this.Depth = (parent?.Depth ?? -1) + 1;
		}

		/// <summary>Returns an untracked JSON value</summary>
		[Pure, MustUseReturnValue]
		public static ObservableJsonValue Untracked(JsonValue value) => new(null, null, default, value);

		public override string ToString()
		{
			return $"{this.GetPath()}: {this.Json.ToString("Q")}";
		}

		/// <summary>Contains the read-only version of this JSON object</summary>
		private JsonValue Json { get; }

		/// <summary>Expose the underlying <see cref="JsonValue"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		public JsonValue ToJson()
		{
			// since we don't know what the caller will do, we have to assume that any content change would trigger a recompute
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json;
		}

		/// <summary>Expose the underlying <see cref="JsonValue"/> of this node, without recording it as a read access</summary>
		/// <remarks>This should only be used by infrastructure that need to inspect the content of the node, outside the "view" of the context</remarks>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public JsonValue GetJsonUnsafe()
		{
			return this.Json;
		}

		internal IObservableJsonContext? Context { get; }

		public IObservableJsonContext? GetContext() => this.Context;

		#region IJsonProxyNode...

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => this.Parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => this.Segment;

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => this.Json.Type;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => this.Depth;

		#endregion

		#region Path...

		private IJsonProxyNode? Parent { get; }

		public IJsonProxyNode? GetParent() => this.Parent;

		/// <summary>Segment of path to this node from its parent</summary>
		private JsonPathSegment Segment { get; }

		/// <summary>Depth from the root to this node</summary>
		/// <remarks>Required to pre-compute the size of path segment arrays</remarks>
		private int Depth { get; }

		/// <summary>Tests if this is the top-level node of the document</summary>
		public bool IsRoot() => this.Depth == 0;

		/// <summary>Returns the depth from the root to this value</summary>
		/// <returns>Number of parents of this value, or 0 if this is the top-level value</returns>
		public int GetDepth() => this.Depth;

		public JsonPath GetPath(JsonPathSegment child)
		{
			return child.TryGetName(out var name) ? GetPath(name)
				: child.TryGetIndex(out var index) ? GetPath(index)
				: GetPath();
		}

		/// <summary>Returns the path to a field of this object, from the root</summary>
		/// <param name="key">Name of a field in this object</param>
		public JsonPath GetPath(string key) => GetPath(key.AsMemory());

		/// <summary>Returns the path to a field of this object, from the root</summary>
		/// <param name="key">Name of a field in this object</param>
		public JsonPath GetPath(ReadOnlyMemory<char> key)
		{
			if (this.IsRoot())
			{
				return JsonPath.Create(new JsonPathSegment(key));
			}

			Span<char> scratch = stackalloc char[32];
			var writer = new JsonPathBuilder(scratch);
			try
			{
				((IJsonProxyNode) this).WritePath(ref writer);
				writer.Append(key);
				return writer.ToPath();
			}
			finally
			{
				writer.Dispose();
			}
		}

		/// <summary>Returns the path to an item of this array, from the root</summary>
		/// <param name="index">Index of the item in this array</param>
		public JsonPath GetPath(int index)
		{
			if (this.IsRoot())
			{
				return JsonPath.Create(index);
			}

			Span<char> scratch = stackalloc char[32];
			var writer = new JsonPathBuilder(scratch);
			try
			{
				((IJsonProxyNode) this).WritePath(ref writer);
				writer.Append(index);
				return writer.ToPath();
			}
			finally
			{
				writer.Dispose();
			}
		}

		/// <summary>Returns the path to an item of this array, from the root</summary>
		/// <param name="index">Index of the item in this array</param>
		public JsonPath GetPath(Index index)
		{
			if (this.IsRoot())
			{
				return JsonPath.Create(index);
			}

			Span<char> scratch = stackalloc char[32];
			// ReSharper disable once NotDisposedResource
			var writer = new JsonPathBuilder(scratch);
			try
			{
				((IJsonProxyNode) this).WritePath(ref writer);
				writer.Append(index);
				return writer.ToPath();
			}
			finally
			{
				writer.Dispose();
			}
		}

		/// <summary>Returns the path of this value, from the root</summary>
		public JsonPath GetPath()
		{
			if (this.IsRoot())
			{
				return JsonPath.Empty;
			}

			Span<char> scratch = stackalloc char[32];
			var builder = new JsonPathBuilder(scratch);
			try
			{
				((IJsonProxyNode) this).WritePath(ref builder);
				return builder.ToPath();
			}
			finally
			{
				builder.Dispose();
			}
		}

		void IJsonProxyNode.WritePath(ref JsonPathBuilder sb)
		{
			this.Parent?.WritePath(ref sb);

			sb.Append(this.Segment);
		}

		public JsonPathSegment[] GetPathSegments(JsonPathSegment child = default)
		{
			var hasChild = !child.IsEmpty();
			var depth = this.Depth;

			if (depth == 0) return hasChild ? [ child ] : [ ];

			var buffer = new JsonPathSegment[depth + (hasChild ? 1 : 0)];
			if (hasChild)
			{
				buffer[depth] = child;
			}
			WritePath(buffer);
			return buffer;
		}

		public bool TryGetPathSegments(Span<JsonPathSegment> buffer, out ReadOnlySpan<JsonPathSegment> segments)
		{
			if (buffer.Length < this.Depth)
			{
				segments = default;
				return false;
			}

			WritePath(buffer);
			segments = buffer.Slice(0, this.Depth);
			return true;
		}

		private void WritePath(Span<JsonPathSegment> buffer)
		{
			IJsonProxyNode node = this;
			while(node.Parent != null)
			{
				buffer[node.Depth - 1] = node.Segment;
				node = node.Parent;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordSelfAccess(ObservableJsonAccess access, JsonValue? value = null)
		{
			this.Context?.RecordRead(this, default, value ?? this.Json, access);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(ReadOnlyMemory<char> key, JsonValue value, ObservableJsonAccess access) => this.Context?.RecordRead(this, new(key), value, access);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(Index index, JsonValue value, ObservableJsonAccess access) => this.Context?.RecordRead(this, new(index), value, access);

		#endregion

		#region IDictionary<TKey, TValue>...

		/// <summary>Number of items in this array</summary>
		public int Count
		{
			get
			{
				// inspecting the value using the debugger may unintentionally trigger a "false read" !
#if DEBUG
				Debugger.NotifyOfCrossThreadDependency();
#endif
				return TryGetCount(out var count) ? count : 0;
				//REVIEW: should we throw if this is not an array or null?
			}
		}

		/// <summary>Try getting the size of this array</summary>
		/// <param name="count">Receives the length of the array, or 0 if this is not an array</param>
		/// <returns><c>true</c> if this is an array; otherwise, <c>false</c></returns>
		public bool TryGetCount(out int count)
		{
			if (this.Json is JsonArray arr)
			{
				//note: 'Length' already implies a check on the type
				RecordSelfAccess(ObservableJsonAccess.Length, this.Json);
				count = arr.Count;
				return true;
			}
			else
			{
				RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
				count = 0;
				return false;
			}
		}

		public bool IsOfType(JsonType type)
		{
			RecordSelfAccess(ObservableJsonAccess.Length, this.Json);
			return this.Json.Type == type;
		}

		public bool IsArrayUnsafe([MaybeNullWhen(false)] out JsonArray value)
		{
			RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
			value = this.Json as JsonArray;
			return value is not null;
		}

		public bool IsObjectUnsafe([MaybeNullWhen(false)] out JsonObject value)
		{
			RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
			value = this.Json as JsonObject;
			return value is not null;
		}


		[Pure]
		public bool IsNullOrMissing()
		{
			RecordSelfAccess(ObservableJsonAccess.Exists);
			return this.Json is JsonNull;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)] 
		public bool Exists() => !this.IsNullOrMissing();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ObservableJsonValue ReturnChild(ReadOnlyMemory<char> key, JsonValue value)
		{
			return this.Context?.FromJson(this, new(key), value) ?? new(null, this, new(key), value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ObservableJsonValue ReturnChild(Index index, JsonValue value)
		{
			return this.Context?.FromJson(this, new(index), value) ?? new(null, this, new(index), value);
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? As<TValue>(TValue? defaultValue = default)
		{
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json.As<TValue>(defaultValue);
		}

		public bool ContainsKey(string key)
		{
			if (!this.Json.TryGetPathValue(key, out var child))
			{
				RecordChildAccess(key.AsMemory(), JsonNull.Missing, ObservableJsonAccess.Exists);
				return false;
			}
			RecordChildAccess(key.AsMemory(), child, ObservableJsonAccess.Exists);
			return true;
		}

		public bool ContainsValue(JsonValue value)
		{
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json switch
			{
				JsonObject obj => obj.Contains(value),
				JsonArray arr => arr.Contains(value),
				_ => false
			};
		}

		public ObservableJsonValue this[string key]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(key);
		}

		public ObservableJsonValue this[JsonPath path]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(path);
		}

		public ObservableJsonValue this[ReadOnlyMemory<char> key]
		{
			[MustUseReturnValue]
			get => Get(key);
		}

		public ObservableJsonValue this[int index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
		}

		public ObservableJsonValue this[Index index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(string key, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(key.AsMemory(), JsonNull.Missing);
				return false;
			}

			value = ReturnChild(key.AsMemory(), child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(ReadOnlyMemory<char> key, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(key, JsonNull.Missing);
				return false;
			}

			value = ReturnChild(key, child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(string key, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(key.AsMemory(), JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(key.AsMemory(), child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(key, JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(key, child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="converter">Converted used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Receives the unpacked value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(string key, IJsonConverter<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(key.AsMemory(), JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(key.AsMemory(), child, ObservableJsonAccess.Value);
			value = converter.Unpack(child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="converter">Converted used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Receives the unpacked value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(ReadOnlyMemory<char> key, IJsonConverter<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(key, JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(key, child, ObservableJsonAccess.Value);
			value = converter.Unpack(child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(int index, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(index, JsonNull.Error);
				return false;
			}

			value = ReturnChild(index, child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(int index, IJsonConverter<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = converter.Unpack(child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		[Pure]
		public bool TryGetValue(Index index, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(index, JsonNull.Error);
				return false;
			}

			value = ReturnChild(index, child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(Index index, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(Index index, IJsonConverter<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = converter.Unpack(child);
			return true;
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(string name) => ReturnChild(name.AsMemory(), this.Json.GetValueOrDefault(name));

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(string name)
		{
			var value = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Value);
			return value;
		}

		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(string name) where TValue : notnull
		{
			var child = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Value);
			if (child.IsNullOrMissing())
			{
				throw new JsonBindingException($"Required JSON field '{GetPath(name)}' was null or missing", GetPath(name), child, typeof(TValue));
			}
			return child.As<TValue>()!;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string name, TValue defaultValue)
		{
			var child = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Value);
			return child.As(defaultValue);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(ReadOnlyMemory<char> name)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualKey);
			name = actualKey?.AsMemory() ?? name;
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
#endif
			return ReturnChild(name, value);
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(ReadOnlyMemory<char> name)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualKey);
			name = actualKey?.AsMemory() ?? name;
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
#endif
			RecordChildAccess(name, value, ObservableJsonAccess.Value);
			return value;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlyMemory<char> name, TValue? defaultValue = default)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualKey);
			name = actualKey?.AsMemory() ?? name;
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
#endif
			RecordChildAccess(name, value, ObservableJsonAccess.Value);
			return value.As(defaultValue);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or an error placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the array</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(int index)
		{
			//note: this fails is not JsonArray or JsonObject!
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the item at the specified location</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or <see cref="JsonNull.Error"/> if the index is out of bounds, of this is not an array.</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(int index)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue? defaultValue = default)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.As(defaultValue);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or an error placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the array</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(Index index)
		{
			//note: this fails is not JsonArray or JsonObject!
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
		}
		
		/// <summary>Returns the underlying <see cref="JsonValue"/> of the item at the specified location</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or <see cref="JsonNull.Error"/> if the index is out of bounds, of this is not an array.</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(Index index)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue? defaultValue = default)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.As(defaultValue);
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(JsonPath path)
		{
			var current = this;
			foreach (var segment in path)
			{
				current = current.Get(segment);
			}
			return current;
		}

		[Pure, MustUseReturnValue]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue GetValue(JsonPath path) => Get(path).ToJson();

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? Get(name)
			 : segment.TryGetIndex(out var index) ? Get(index)
			 : this;

		[Pure, MustUseReturnValue]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue GetValue(JsonPathSegment segment) => Get(segment).ToJson();

		/// <summary>Returns a new instance with the same path, but with the different value </summary>
		/// <param name="newValue">New value of this instance</param>
		/// <returns>Equivalent instance, that could replace the current one in its parent</returns>
		public ObservableJsonValue Visit(JsonValue? newValue)
		{
			newValue ??= JsonNull.Null;
			if (ReferenceEquals(this.Json, newValue))
			{
				return this;
			}
			return this.Context?.FromJson(this.Parent, this.Segment, newValue) ?? new(null, this.Parent, this.Segment, newValue);
		}

		#endregion

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.ToJson().JsonSerialize(writer);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.ToJson();

	}

}
