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
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;

	/// <summary>Observable JSON Object</summary>
	[DebuggerDisplay("Count={Count}, Path={ToString(),nq}")]
	public sealed class ObservableJsonValue : IJsonSerializable, IJsonPackable
	{

		public ObservableJsonValue(IObservableJsonTransaction tr, ObservableJsonPath path, JsonValue json)
		{
			Contract.Debug.Requires(tr != null && json != null);
			this.Transaction = tr;
			this.Path = path;
			this.Json = json;
		}

		public override string ToString()
		{
			var path = ObservableJson.ComputePath(this, null, null);
			return path + ": " + this.Json.ToString("Q");
		}

		/// <summary>Contains the read-only version of this JSON object</summary>
		public JsonValue Json { get; private set; }

		internal IObservableJsonTransaction Transaction { get; }

		public IObservableJsonTransaction GetTransaction() => this.Transaction;

		/// <summary>Path of this instance, relative to its parent</summary>
		public ObservableJsonPath Path { get; }

		#region IDictionary<TKey, TValue>...

		/// <summary>Number of fields in this object</summary>
		public int Count => this.Json switch
		{
			JsonObject obj => obj.Count,
			JsonArray arr  => arr.Count,
			_              => 0 // what should we do here?
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)] 
		public bool IsNullOrMissing() => this.Json is JsonNull;

		public bool Exists() => this.Json is not JsonNull;

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? As<TValue>(TValue? defaultValue = default) => this.Json.As<TValue>(defaultValue);

		public bool ContainsKey(string key) => this.Json is JsonObject obj && obj.ContainsKey(key);

		public bool ContainsValue(JsonValue value) => this.Json switch
		{
			JsonObject obj => obj.Contains(value),
			JsonArray arr  => arr.Contains(value),
			_              => false
		};

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
			//set => Set(index, value.Json);
		}

		public ObservableJsonValue this[Index index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
			//set => Set(index, value.Json);
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
		public bool TryGetValue(string key, out ObservableJsonValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child))
			{
				value = this.Transaction.FromJson(this, key, JsonNull.Missing);
				return false;
			}

			value = this.Transaction.FromJson(this, key, child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root.TryGetValue&lt;Foo>("foo", out var foo))
		/// { // first time
		///		foo = new Foo { /* ... */ }
		///     root.Set("foo", foo);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(string key, [MaybeNullWhen(false)] out TValue value)
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
		public bool TryGetValue(int index, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = this.Transaction.FromJson(this, index, JsonNull.Error);
				return false;
			}

			value = this.Transaction.FromJson(this, index, child);
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
		public bool TryGetValue(Index index, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = this.Transaction.FromJson(this, index, JsonNull.Error);
				return false;
			}

			value = this.Transaction.FromJson(this, index, child);
			return true;
		}

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(string key) => this.Transaction.FromJson(this, key, this.Json.GetValueOrDefault(key));

		[Pure, MustUseReturnValue]
		public TValue? Get<TValue>(string key) => this.Json.GetValueOrDefault(key).As<TValue>(default(TValue));

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(ReadOnlySpan<char> key)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing, out var actualKey);
			return this.Transaction.FromJson(this, actualKey ?? key.ToString(), value);
#else
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing);
			return this.Transaction.FromJson(this, key.ToString(), value);
#endif
		}

		[Pure, MustUseReturnValue]
		public TValue? Get<TValue>(ReadOnlySpan<char> key) => this.Json.GetValueOrDefault(key).As<TValue>(default(TValue));

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(ReadOnlyMemory<char> key)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing, out var actualKey);
			return this.Transaction.FromJson(this, actualKey ?? key.GetStringOrCopy(), value);
#else
			var value = this.Json.GetValueOrDefault(key, JsonNull.Missing);
			return this.Transaction.FromJson(this, key.GetStringOrCopy(), value);
#endif
		}

		[Pure, MustUseReturnValue]
		public TValue? Get<TValue>(ReadOnlyMemory<char> key) => this.Json.GetValueOrDefault(key).As<TValue>(default(TValue));

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(int index) => this.Transaction.FromJson(this, index, this.Json.GetValueOrDefault(index));

		[Pure, MustUseReturnValue]
		public TValue? Get<TValue>(int index) => this.Json.GetValueOrDefault(index).As<TValue>(default(TValue));

		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(Index index) => this.Transaction.FromJson(this, index, this.Json.GetValueOrDefault(index));

		[Pure, MustUseReturnValue]
		public TValue? Get<TValue>(Index index) => this.Json.GetValueOrDefault(index).As<TValue>(default(TValue));

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

		private static void InsertInParent(in ObservableJsonPath path, ObservableJsonValue value)
		{
			Contract.Debug.Assert(path.Parent != null);
			if (path.Key != null)
			{
				path.Parent.Set(path.Key, value.Json);
			}
			else if (path.Index != null)
			{
				path.Parent.Set(path.Index.Value, value.Json);
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

				var res = this.Transaction.NewObject(this.Path);
				InsertInParent(this.Path, res);
				json = res.Json;
				this.Json = json;
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

				var res = this.Transaction.NewArray(this.Path);
				ObservableJsonValue.InsertInParent(this.Path, res);
				json = res.Json;
				this.Json = json;
			}
			if (json is not JsonArray arr)
			{
				throw new InvalidOperationException($"Cannot add a field to a JSON {json.Type}");
			}
			return arr;
		}

		internal void NotifyChildChanged(ObservableJsonValue child, string? key, Index? index)
		{
			JsonValue newJson;
			if (key != null)
			{
				Contract.Debug.Assert(index == null);
				var prevObj = RealizeObjectIfRequired();
				if (prevObj.IsReadOnly)
				{ // first mutation
					prevObj = JsonObject.Copy(prevObj, deep: false, readOnly: false);
					prevObj[key] = child.Json;
					newJson = prevObj;
				}
				else
				{ // additional mutation
					prevObj[key] = child.Json;
					return;
				}
			}
			else if (index != null)
			{
				var prevArr = RealizeArrayIfRequired();
				if (prevArr.IsReadOnly)
				{
					prevArr = JsonArray.Copy(prevArr, deep: false, readOnly: false);
					prevArr[index.Value] = child.Json;
					newJson = prevArr;
				}
				else
				{ // additional mutation
					prevArr[index.Value] = child.Json;
					return;
				}
			}
			else
			{
				throw new NotSupportedException();
			}

			this.Json = newJson;
			this.Path.NotifyParent(this);
		}

		internal enum InsertionBehavior : byte
		{
			None,
			OverwriteExisting,
			ThrowOnExisting,
		}

		/// <summary>Changes the value of the current instance in its parent</summary>
		internal bool InsertOrUpdate(JsonValue? value, InsertionBehavior behavior)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var parent = this.Path.Parent;
			if (parent == null) throw new InvalidOperationException("Cannot replace the top level value");

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
						throw new ArgumentException("The key already exist in the document");
					}
					default:
					{
						if (prevJson.Equals(value))
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
				case (JsonNull before, JsonNull after):
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
			this.Path.NotifyParent(this);

			if (this.Path.Key != null)
			{
				if (patch == null)
				{
					this.Transaction.RecordUpdate(parent, this.Path.Key, value);
				}
				else
				{
					this.Transaction.RecordPatch(parent, this.Path.Key, patch);
				}
			}
			else if (this.Path.Index != null)
			{
				var idx = this.Path.Index.Value;
				// the pattern "[^0]" is used to append a new item to an array
				// -> since the parent now contains the appended item, its Count is already incremented, so we have to replace it with "^1" instead!
				if (idx.Equals(^0))
				{
					idx = ^1;
				}
				var index = idx.GetOffset(parent.Count);
				if (patch == null)
				{
					this.Transaction.RecordUpdate(parent, index, value);
				}
				else
				{
					this.Transaction.RecordPatch(parent, index, patch);
				}
			}
			return true;
		}

		internal bool InsertOrUpdate(string key, JsonValue? value, InsertionBehavior behavior)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = RealizeObjectIfRequired();

			var prevJson = selfJson.GetValueOrDefault(key);
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
						throw new ArgumentException("The key already exist in the document");
					}
					default:
					{
						if (prevJson.Equals(value))
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
					patch = before.ComputePatch(after);
					break;
				}
				case (JsonArray before, JsonArray after):
				{ // Array patch
					patch = before.ComputePatch(after);
					break;
				}
				case (_, JsonNull):
				{ // delete
					this.Transaction.RecordDelete(this, key);
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
			newJson[key] = value;

			// notify the parent chain if we changed the current json instance
			if (!ReferenceEquals(this.Json, newJson))
			{
				this.Json = newJson;
				this.Path.NotifyParent(this);
			}

			if (patch == null)
			{
				this.Transaction.RecordUpdate(this, key, value);
			}
			else
			{
				this.Transaction.RecordPatch(this, key, patch);
			}
			return true;
		}

		internal bool InsertOrUpdate(Index index, JsonValue? value, InsertionBehavior behavior)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = this.RealizeArrayIfRequired();

			var prevJson = selfJson.GetValueOrDefault(index);
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
						throw new ArgumentException("The key already exist in the document");
					}
					default:
					{
						if (prevJson.Equals(value))
						{ // idempotent
							return true;
						}
						break;
					}
				}
			}

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
				this.Path.NotifyParent(this);
			}

			if (patch == null)
			{
				this.Transaction.RecordUpdate(this, offset, value);
			}
			else
			{
				this.Transaction.RecordPatch(this, offset, patch);
			}
			return true;
		}

		private JsonValue Convert<T>(T? value) => JsonValue.ReadOnly.FromValue(value); //TODO: pass the parent settings?

		#region Set(value)

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		/// <remarks>
		/// <para>If the current element is a field of an object, the field will either be added or updated in the parent object.</para>
		/// <para>If the current element is an index in an array, the entry will be set to the new value. If the array was smaller, it will automatically be expanded, and any gaps will be filled with null values.</para>
		/// </remarks>
		public void Set(JsonValue? value) => InsertOrUpdate(value, InsertionBehavior.OverwriteExisting);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonNull? value) => Set((JsonValue?) value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonBoolean? value) => Set((JsonValue?) value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonNumber? value) => Set((JsonValue?) value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonString? value) => Set((JsonValue?) value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonDateTime? value) => Set((JsonValue?) value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonObject? value) => Set((JsonValue?) value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonArray? value) => Set((JsonValue?) value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(ObservableJsonValue? value) => Set(value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(TValue? value) => Set(Convert<TValue>(value));

		#endregion

		#region Set(key, value)

		public void Set(string key, JsonValue? value) => InsertOrUpdate(key, value, InsertionBehavior.OverwriteExisting);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, JsonNull? value) => Set(key, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, JsonBoolean? value) => Set(key, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, JsonNumber? value) => Set(key, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, JsonString? value) => Set(key, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, JsonDateTime? value) => Set(key, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, JsonObject? value) => Set(key, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, JsonArray? value) => Set(key, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(string key, ObservableJsonValue? value) => Set(key, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(string key, TValue? value) => Set(key, Convert<TValue>(value));

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
		public void Set(JsonPath path, ObservableJsonValue? value) => Set(path, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(JsonPath path, TValue? value) => Set(path, Convert<TValue>(value)); //TODO: pass the parent settings?

		#endregion

		#region Set(index, value)

		public void Set(int index, JsonValue? value) => InsertOrUpdate(index, value, InsertionBehavior.OverwriteExisting);

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
		public void Set(int index, ObservableJsonValue? value) => Set(index, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(int index, TValue? value) => Set(index, Convert<TValue>(value)); //TODO: pass the parent settings?

		public void Set(Index index, JsonValue? value) => InsertOrUpdate(index, value, InsertionBehavior.OverwriteExisting);

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
		public void Set(Index index, ObservableJsonValue? value) => Set(index, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(Index index, TValue? value) => Set(index, Convert<TValue>(value)); //TODO: pass the parent settings?

		#endregion

		/// <summary>Adds a new value to the parent object or array</summary>
		/// <param name="value">Value of the new field or item</param>
		/// <remarks>
		/// <para>If the current element is null or missing, it will automatically be promoted to an array with <paramref name="value"/> as its single element.</para>
		/// <para>If the current element is a field of an object, then it will be created in the parent object, unless it already has a non-null value, in which case an exception will be thrown.</para>
		/// <para>If the current element is an index in an array, then it will be filled with <paramref name="value"/>, unless it previously had a non-null value, in which case an exception will be thrown.</para>
		/// </remarks>
		/// <exception cref="ArgumentException">If the current element already exists.</exception>
		public void Add(JsonValue? value) => InsertOrUpdate(value, InsertionBehavior.ThrowOnExisting);

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
		public void Add(ObservableJsonValue? value) => Add(value?.Json);

		/// <summary>Adds a new field to the object</summary>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add<TValue>(TValue value) => Add(Convert<TValue>(value));

		/// <summary>Adds a new field to the object</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string key, JsonValue value) => Get(key).Add(value);

		/// <summary>Adds a new field to the object</summary>
		/// <param name="path">Name of the field</param>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonPath path, JsonValue value) => Get(path).Add(value);

		/// <summary>Adds a new field to the object</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string key, ObservableJsonValue? value) => Get(key).Add(value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(string key, JsonValue? value) => Get(key).InsertOrUpdate(value, InsertionBehavior.None);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(string key, ObservableJsonValue? value) => Get(key).InsertOrUpdate(value?.Json, InsertionBehavior.None);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(JsonPath path, JsonValue? value) => Get(path).InsertOrUpdate(value, InsertionBehavior.None);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(JsonPath path, ObservableJsonValue? value) => Get(path).InsertOrUpdate(value?.Json, InsertionBehavior.None);

		public void Remove()
		{
			// simulate the value to be Missing
			this.Json = JsonNull.Missing;
			if (this.Path.Key != null)
			{
				this.Path.Parent?.Remove(this.Path.Key);
			}
			else
			{
				Contract.Debug.Requires(this.Path.Index != null);
				this.Path.Parent?.Remove(this.Path.Index.Value);
			}
		}

		public bool Remove(string key)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.ContainsKey(key))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(key);
			this.Json = newJson;
			this.Path.NotifyParent(this);
			this.Transaction.RecordDelete(this, key);
			return true;
		}

		public bool Remove(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.TryGetValue(key, out value))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(key);
			this.Json = newJson;
			this.Path.NotifyParent(this);
			this.Transaction.RecordDelete(this, key);
			return true;
		}

		public bool Remove(int index)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			if ((uint) index >= prevJson.Count)
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(index);
			this.Json = newJson;
			this.Path.NotifyParent(this);
			this.Transaction.RecordDelete(this, index);
			return true;
		}

		public bool Remove(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			if ((uint) index >= prevJson.Count)
			{ // not found
				value = default;
				return false;
			}

			value = prevJson[index];
			var newJson = prevJson.CopyAndRemove(index);
			this.Json = newJson;
			this.Path.NotifyParent(this);
			this.Transaction.RecordDelete(this, index);
			return true;
		}

		public bool Remove(Index index)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			var offset = index.GetOffset(prevJson.Count);
			if ((uint) offset >= prevJson.Count)
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(offset);
			this.Json = newJson;
			this.Path.NotifyParent(this);
			this.Transaction.RecordDelete(this, offset);
			return true;
		}

		public bool Remove(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			var offset = index.GetOffset(prevJson.Count);
			if ((uint) offset >= prevJson.Count)
			{ // not found
				value = default;
				return false;
			}

			value = prevJson[offset];
			var newJson = prevJson.CopyAndRemove(offset);
			this.Json = newJson;
			this.Path.NotifyParent(this);
			this.Transaction.RecordDelete(this, offset);
			return true;
		}

		private static void Increment(ObservableJsonValue value)
		{
			switch (value.Json)
			{
				case JsonNumber num:
				{
					value.Set(num + 1);
					break;
				}
				case JsonNull:
				{
					value.Set(JsonNumber.One);
					break;
				}
				default:
				{
					throw new InvalidOperationException($"Cannot increment '{value.Path.ToString()}' because it is a {value.Json.Type}");
				}
			}
		}

		public void Increment()
		{
			Increment(this);
		}

		public ObservableJsonValue Increment(string key)
		{
			var prev = Get(key);
			Increment(prev);
			return prev;
		}

		public ObservableJsonValue Increment(int index)
		{
			var prev = Get(index);
			Increment(prev);
			return prev;
		}

		public ObservableJsonValue Increment(Index index)
		{
			var prev = Get(index);
			Increment(prev);
			return prev;
		}

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

		public TValue? Exchange<TValue>(TValue? value)
		{
			var prev = this.As<TValue>();
			Set(Convert<TValue>(value));
			return prev;
		}

		public JsonValue Exchange(string key, JsonValue? value)
		{
			var prev = this.Json[key];
			Set(key, value);
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

		public TValue? Exchange<TValue>(string key, TValue? value)
		{
			var prev = Get<TValue>(key);
			Set(key, value);
			return prev;
		}

		public TValue? Exchange<TValue>(int index, TValue? value)
		{
			var prev = Get<TValue>(index);
			Set(index, value);
			return prev;
		}

		public TValue? Exchange<TValue>(Index index, TValue? value)
		{
			var prev = Get<TValue>(index);
			Set(index, value);
			return prev;
		}

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
					newJson = JsonArray.EmptyReadOnly;
					break;
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
			this.Json = newJson;
			this.Path.NotifyParent(this);
			this.Transaction.RecordClear(this);
		}

		public ObservableJsonValue GetOrCreateObject(string path)
		{
			var child = this.Get(path);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonObject.EmptyReadOnly);
			}
			return child;
		}

		public ObservableJsonValue GetOrCreateArray(string path)
		{
			var child = this.Get(path);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonArray.EmptyReadOnly);
			}
			return child;
		}

		#endregion

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.Json.JsonSerialize(writer);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.Json;

	}

}
