#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Immutable JSON Object</summary>
	[Serializable]
	[DebuggerDisplay("JSON Object[{Count}] {GetCompactRepresentation(0),nq}")]
	[DebuggerTypeProxy(typeof(ImmutableJsonObject.DebugView))]
	[DebuggerNonUserCode]
	public readonly struct ImmutableJsonObject : IImmutableDictionary<string, JsonValue>, IJsonPackable, IJsonSerializable, ISliceSerializable
	{

		private readonly JsonObject? Items;

		public static readonly ImmutableJsonObject Empty = default;

		private static readonly JsonObject None = new(0, StringComparer.Ordinal);

		public static ImmutableJsonObject Create(string key0, JsonValue? value0) => new(JsonObject.Create(key0, value0));

		public static ImmutableJsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1) => new(JsonObject.Create(key0, value0, key1, value1));

		public static ImmutableJsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1, string key2, JsonValue? value2) => new(JsonObject.Create(key0, value0, key1, value1, key2, value2));

		public static ImmutableJsonObject Create(JsonObject items) => new(items.Copy(deep: true));

		public static ImmutableJsonObject Create(IDictionary<string, JsonValue?> items) => new(JsonObject.Copy(items, deep: false));

		public static ImmutableJsonObject Create(IEnumerable<KeyValuePair<string, JsonValue?>> items) => new(JsonObject.Copy(items, deep: false));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ImmutableJsonObject()
		{
			this.Items = default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ImmutableJsonObject(JsonObject items)
		{
			this.Items = items;
		}

		public int Count => this.Items?.Count ?? 0;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsKey(string key) => this.Items?.ContainsKey(key) ?? false;

		bool IImmutableDictionary<string, JsonValue>.Contains(KeyValuePair<string, JsonValue> pair) => this.Items != null && this.Items.TryGetValue(pair.Key, out var obj) && obj.Equals(pair.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonValue value) => (this.Items ?? None).TryGetValue(key, out value);

		public JsonValue this[string key] => (this.Items ?? None)[key];

		public Dictionary<string, JsonValue>.Enumerator GetEnumerator() => (this.Items ?? None).GetEnumerator();
		IEnumerator<KeyValuePair<string, JsonValue>> IEnumerable<KeyValuePair<string, JsonValue>>.GetEnumerator() => throw new NotImplementedException();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public Dictionary<string, JsonValue>.KeyCollection Keys => (this.Items ?? None).Keys;
		IEnumerable<string> IReadOnlyDictionary<string, JsonValue>.Keys => (this.Items ?? None).Keys;

		public Dictionary<string, JsonValue>.ValueCollection Values => (this.Items ?? None).Values;
		IEnumerable<JsonValue> IReadOnlyDictionary<string, JsonValue>.Values => (this.Items ?? None).Values;

		[System.Diagnostics.Contracts.Pure]
		public ImmutableJsonObject Add(string key, JsonValue value)
		{
			if (this.Items == null) return new(JsonObject.Create(key, value));
			var items = this.Items.Copy(deep: true);
			items.Add(key, value);
			return new(items);
		}
		IImmutableDictionary<string, JsonValue> IImmutableDictionary<string, JsonValue>.Add(string key, JsonValue value) => Add(key, value);

		[System.Diagnostics.Contracts.Pure]
		public ImmutableJsonObject AddRange(IEnumerable<KeyValuePair<string, JsonValue>> pairs)
		{
			var items = this.Items?.Copy(deep: true) ?? new JsonObject(pairs.TryGetNonEnumeratedCount(out var count) ? count : 0);
			items.AddRange(pairs);
			return new(items);
		}
		IImmutableDictionary<string, JsonValue> IImmutableDictionary<string, JsonValue>.AddRange(IEnumerable<KeyValuePair<string, JsonValue>> pairs) => this.AddRange(pairs);

		[System.Diagnostics.Contracts.Pure]
		public ImmutableJsonObject Clear() => Empty;
		IImmutableDictionary<string, JsonValue> IImmutableDictionary<string, JsonValue>.Clear() => Empty;

		[System.Diagnostics.Contracts.Pure]
		public ImmutableJsonObject Remove(string key)
		{
			if (this.Items == null) return Empty;
			var items = this.Items.Copy(deep: true);
			items.Remove(key);
			return new ImmutableJsonObject(items);
		}
		IImmutableDictionary<string, JsonValue> IImmutableDictionary<string, JsonValue>.Remove(string key) => Remove(key);

		public ImmutableJsonObject RemoveRange(IEnumerable<string> keys)
		{
			if (this.Items == null) return Empty;
			var items = this.Items.Copy(deep: true);
			foreach (var key in keys)
			{
				items.Remove(key);
			}
			return new(items);
		}
		IImmutableDictionary<string, JsonValue> IImmutableDictionary<string, JsonValue>.RemoveRange(IEnumerable<string> keys) => this.RemoveRange(keys);

		[System.Diagnostics.Contracts.Pure]
		public ImmutableJsonObject SetItem(string key, JsonValue value)
		{
			if (this.Items == null) return new(JsonObject.Create(key, value));
			var items = this.Items.Copy(deep: true);
			items[key] = value ?? JsonNull.Null;
			return new(items);
		}
		IImmutableDictionary<string, JsonValue> IImmutableDictionary<string, JsonValue>.SetItem(string key, JsonValue value) => this.SetItem(key, value);

		[System.Diagnostics.Contracts.Pure]
		public ImmutableJsonObject SetItems(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			if (this.Items == null)
			{
				return new(JsonObject.Copy(items!, deep: false));
			}

			var xs = this.Items.Copy(deep: true);
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				xs.EnsureCapacity(xs.Count + count);
			}
			foreach (var item in items)
			{
				xs[item.Key] = item.Value ?? JsonNull.Null;
			}
			return new(xs);

		}
		IImmutableDictionary<string, JsonValue> IImmutableDictionary<string, JsonValue>.SetItems(IEnumerable<KeyValuePair<string, JsonValue>> items) => this.SetItems(items);

		public bool TryGetKey(string equalKey, out string actualKey) => throw new NotImplementedException();

		[System.Diagnostics.Contracts.Pure]
		public ImmutableJsonObject Set(string key, JsonValue value)
		{
			var items = this.Items?.Copy(deep: true) ?? new JsonObject();
			items[key] = value;
			return new ImmutableJsonObject(items);
		}

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.Items?.Copy(deep: true) ?? new JsonObject();
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => (this.Items ?? None).JsonSerialize(writer);
		void IJsonSerializable.JsonDeserialize(JsonObject value, Type declaredType, ICrystalJsonTypeResolver resolver) => throw new NotSupportedException();
		public void WriteTo(ref SliceWriter writer) => (this.Items ?? None).WriteTo(ref writer);

		internal string GetCompactRepresentation(int depth) => (this.Items ?? None).GetCompactRepresentation(depth);

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

	}

}
