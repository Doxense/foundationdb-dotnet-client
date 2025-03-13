#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	/// <summary>Observable JSON Object that will capture all reads</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class ObservableJsonValue : IJsonSerializable, IJsonPackable
	{

		public ObservableJsonValue(IObservableJsonContext ctx, ObservableJsonValue? parent, JsonPathSegment path, JsonValue json)
		{
			Contract.Debug.Requires(ctx != null && json != null);
			this.Context = ctx;
			this.Parent = parent;
			this.Path = path;
			this.Json = json;
			this.Depth = (this.Parent?.Depth ?? -1) + 1;
		}

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

		internal IObservableJsonContext Context { get; }

		public IObservableJsonContext GetContext() => this.Context;

		#region Path...

		private ObservableJsonValue? Parent { get; }

		/// <summary>Segment of path to this node from its parent</summary>
		private JsonPathSegment Path { get; }

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
				PrependPath(ref writer);
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
				PrependPath(ref writer);
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
				PrependPath(ref writer);
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
				PrependPath(ref builder);
				return builder.ToPath();
			}
			finally
			{
				builder.Dispose();
			}
		}

		private void PrependPath(ref JsonPathBuilder sb)
		{
			this.Parent?.PrependPath(ref sb);

			sb.Append(this.Path);
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
			int depth = this.Depth;
			if (depth > 0)
			{
				buffer[depth - 1] = this.Path;
				this.Parent!.WritePath(buffer);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordSelfAccess(ObservableJsonAccess access, JsonValue? value = null)
		{
			this.Context.RecordRead(this, default, value ?? this.Json, access);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(ReadOnlyMemory<char> key, JsonValue value, ObservableJsonAccess access) => this.Context.RecordRead(this, new(key), value, access);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(Index index, JsonValue value, ObservableJsonAccess access) => this.Context.RecordRead(this, new(index), value, access);

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
				RecordSelfAccess(ObservableJsonAccess.Length, this.Json);
				return this.Json is JsonArray arr ? arr.Count : 0;
			}
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
			return this.Context.FromJson(this, new(key), value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ObservableJsonValue ReturnChild(Index index, JsonValue value)
		{
			return this.Context.FromJson(this, new(index), value);
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
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
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
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
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

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
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

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(string key)
		{
			return ReturnChild(key.AsMemory(), this.Json.GetValueOrDefault(key));
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string key, TValue? defaultValue = default)
		{
			var child = this.Json.GetValueOrDefault(key);
			RecordChildAccess(key.AsMemory(), child, ObservableJsonAccess.Value);
			return child.As(defaultValue);
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(ReadOnlyMemory<char> key)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing, out var actualKey);
			key = actualKey?.AsMemory() ?? key;
#else
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing);
#endif
			return this.Context.FromJson(this, new(key), value);
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlyMemory<char> key, TValue? defaultValue = default)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing, out var actualKey);
			key = actualKey?.AsMemory() ?? key;
#else
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing);
#endif
			RecordChildAccess(key, value, ObservableJsonAccess.Value);
			return value.As(defaultValue);
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(int index)
		{
			//note: this fails is not JsonArray or JsonObject!
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
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

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(Index index)
		{
			//note: this fails is not JsonArray or JsonObject!
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
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
		public ObservableJsonValue Get(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? Get(name)
			 : segment.TryGetIndex(out var index) ? Get(index)
			 : this;

		#endregion

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.ToJson().JsonSerialize(writer);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.ToJson();

	}

}
