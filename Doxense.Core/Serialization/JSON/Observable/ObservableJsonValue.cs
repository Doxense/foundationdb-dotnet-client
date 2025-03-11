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
	[DebuggerDisplay("Count={Count}, Path={ToString(),nq}")]
	public sealed class ObservableJsonValue : IJsonSerializable, IJsonPackable
	{

		public ObservableJsonValue(IObservableJsonContext ctx, ObservableJsonValue? parent, ReadOnlyMemory<char> key, Index? index, JsonValue json)
		{
			Contract.Debug.Requires(ctx != null && json != null);
			this.Context = ctx;
			this.Parent = parent;
			this.Key = key;
			this.Index = index;
			this.Json = json;
		}

		public override string ToString()
		{
			return $"{this.GetPath()}: {this.Json.ToString("Q")}";
		}

		/// <summary>Contains the read-only version of this JSON object</summary>
		private JsonValue Json { get; }

		public JsonValue ToJson()
		{
			RecordSelfAccess(existOnly: false);
			return this.Json;
		}

		internal IObservableJsonContext Context { get; }

		public IObservableJsonContext GetContext() => this.Context;

		#region Path...

		private ObservableJsonValue? Parent { get; }

		/// <summary>Name of the field that contains this value in its parent object, or <see langword="null"/> if it was not part of an object</summary>
		private ReadOnlyMemory<char> Key { get; }

		/// <summary>Position of this value in its parent array, or <see langword="null"/> if it was not part of an array</summary>
		private Index? Index { get; }
		//REVIEW: maybe not nullable? if Key != null then Index should be ignored (0), and if Key == null, then it is present?

		/// <summary>Tests if this is the top-level node of the document</summary>
		public bool IsRoot() => this.Key.Length == 0 && this.Index == null;

		/// <summary>Returns the path to a field of this object, from the root</summary>
		/// <param name="key">Name of a field in this object</param>
		public JsonPath GetPath(string key) => GetPath(key.AsMemory());

		/// <summary>Returns the path to a field of this object, from the root</summary>
		/// <param name="key">Name of a field in this object</param>
		public JsonPath GetPath(ReadOnlyMemory<char> key)
		{
			if (this.IsRoot())
			{
				return JsonPath.Empty[key];
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
				return JsonPath.Empty[index];
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
				return JsonPath.Empty[index];
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

			if (this.Index != null)
			{
				sb.Append(this.Index.Value);
			}
			else if (this.Key.Length != 0)
			{
				sb.Append(this.Key.Span);
			}
		}

		#endregion

		#region IDictionary<TKey, TValue>...

		/// <summary>Number of fields in this object</summary>
		public int Count
		{
			get
			{
				// inspecting the value using the debugger may unintentionally trigger a "false read" !
#if DEBUG
				Debugger.NotifyOfCrossThreadDependency();
#endif
				RecordLengthAccess();
				return this.Json switch
				{
					JsonObject obj => obj.Count,
					JsonArray arr => arr.Count,
					_ => 0 // what should we do here?
				};
			}
		}

		[Pure] 
		public bool IsNullOrMissing()
		{
			RecordSelfAccess(existOnly: true);
			return this.Json is JsonNull;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)] 
		public bool Exists() => !this.IsNullOrMissing();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordSelfAccess(bool existOnly)
		{
			// only register access to the root
			if (this.Parent == null)
			{
				this.Context.RecordRead(this, null, this.Json, existOnly);
			}
			else if (this.Index != null)
			{
				this.Context.RecordRead(this.Parent, this.Index.GetValueOrDefault(), this.Json, existOnly);
			}
			else
			{
				this.Context.RecordRead(this.Parent, this.Key, this.Json, existOnly);
			}
		}

		private void RecordLengthAccess()
		{
			this.Context.RecordLength(this, this.Json);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(ReadOnlyMemory<char> key, JsonValue value, bool existOnly) => this.Context.RecordRead(this, key, value, existOnly);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(Index index, JsonValue value, bool existOnly) => this.Context.RecordRead(this, index, value, existOnly);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ObservableJsonValue ReturnChild(ReadOnlyMemory<char> key, JsonValue value)
		{
			return this.Context.FromJson(this, key, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ObservableJsonValue ReturnChild(Index index, JsonValue value)
		{
			return this.Context.FromJson(this, index, value);
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? As<TValue>(TValue? defaultValue = default)
		{
			RecordSelfAccess(existOnly: false);
			return this.Json.As<TValue>(defaultValue);
		}

		public bool ContainsKey(string key)
		{
			if (!this.Json.TryGetPathValue(key, out var child))
			{
				RecordChildAccess(key.AsMemory(), JsonNull.Missing, existOnly: false);
				return false;
			}
			RecordChildAccess(key.AsMemory(), child, existOnly: false);
			return true;
		}

		public bool ContainsValue(JsonValue value)
		{
			RecordSelfAccess(existOnly: false);
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
				RecordChildAccess(key.AsMemory(), JsonNull.Missing, existOnly: false);
				value = default;
				return false;
			}

			RecordChildAccess(key.AsMemory(), child, existOnly: false);
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
			RecordChildAccess(key.AsMemory(), child, existOnly: false);
			return child.As(defaultValue);
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(ReadOnlyMemory<char> key)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing, out var actualKey);
			return this.Context.FromJson(this, actualKey?.AsMemory() ?? key, value);
#else
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing);
			return this.Context.FromJson(this, key, value);
#endif
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlyMemory<char> key, TValue? defaultValue = default)
		{
#if NET9_0_OR_GREATER
			var child = this.Json.GetValueOrDefault(key, JsonNull.Missing, out var actualKey);
			RecordChildAccess(actualKey?.AsMemory() ?? key, child, existOnly: false);
#else
			var child = this.Json.GetValueOrDefault(key, JsonNull.Missing);
			RecordChildAccess(key, child, existOnly: false);
#endif
			return child.As(defaultValue);
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(int index)
		{
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue? defaultValue = default)
		{
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, existOnly: false);
			return child.As(defaultValue);
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(Index index)
		{
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue? defaultValue = default)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, existOnly: false);
			return child.As(defaultValue);
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(JsonPath path)
		{
			var current = this;
			foreach (var (parent, key, index, last) in path)
			{
				if (key.Length > 0)
				{
					current = current.Get(key);
				}
				else
				{
					current = current.Get(index);
				}
			}
			return current;
		}

		#endregion

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.ToJson().JsonSerialize(writer);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.ToJson();

	}

}
