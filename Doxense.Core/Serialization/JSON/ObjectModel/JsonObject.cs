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

#define CHECK_INVARIANTS

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Dynamic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Runtime.Serialization;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;

	/// <summary>JSON Object with fields</summary>
	[Serializable]
	[DebuggerDisplay("JSON Object[{Count}]{GetMutabilityDebugLiteral(),nq} {GetCompactRepresentation(0),nq}")]
	[DebuggerTypeProxy(typeof(JsonObject.DebugView))]
	[DebuggerNonUserCode]
	[JetBrains.Annotations.PublicAPI]
	public sealed class JsonObject : JsonValue, IDictionary<string, JsonValue>, IReadOnlyDictionary<string, JsonValue>, IEquatable<JsonObject>
	{
		// A JSON object can be writable (mutable), or read-only (immutable)
		// - Writable means that items can be added or removed from the "top-level" of this object.
		// - Read-only means that no items can be added or removed, BUT it does not mean that any children is itself readonly!
		// A JSON object can track the mutability of its children, and will maintain a flag it there was at least one mutable children at some point.
		// - We could track and update this state in real-time, but we are mostly interested in keeping track of readonly objects that were immutable from the moment of creation.

		/// <summary>Map of the properties of this object</summary>
		/// <remarks>If <see cref="m_readOnly"/> is not <see langword="0"/>, then any attempt to modify this dictionary should throw an exception</remarks>
		private readonly Dictionary<string, JsonValue> m_items;

		/// <summary>Defines the mutability of this object.</summary>
		/// <remarks>Mutability can change from Immutable to any of the ReadOnlyXYZ variants, but not the over way arround!</remarks>
		private bool m_readOnly;

		/// <summary>Returns a new empty JSON object</summary>
		[Obsolete("Use JsonObject.Create() for a mutable empty object, or JsonObject.EmptyReadOnly for an immutable emtpy singleton")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject Empty => new(new Dictionary<string, JsonValue>(0, StringComparer.Ordinal), readOnly: false);

		/// <summary>Empty read-only JSON object singleton</summary>
		/// <remarks>This instance cannot be modified, and should be used to reduce memory allocations when working with read-only JSON</remarks>
		public static readonly JsonObject EmptyReadOnly = new(new Dictionary<string, JsonValue>(0, StringComparer.Ordinal), readOnly: true);

		[JetBrains.Annotations.UsedImplicitly(JetBrains.Annotations.ImplicitUseTargetFlags.WithMembers)]
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

		#region Constructors...

		/// <summary>Creates a new JSON object that is empty</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject()
			: this(0, StringComparer.Ordinal)
		{ }

		/// <summary>Creates a new JSON object that is empty and has the specified capacity</summary>
		/// <param name="capacity">The initial number of elements that the <see cref="JsonObject" /> can contain.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject(int capacity)
			: this(capacity, StringComparer.Ordinal)
		{ }

		/// <summary>Creates a new JSON object that is empty, and uses the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="StringComparer.Ordinal">ordinal string comparer</see>.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject(IEqualityComparer<string>? comparer) //REVIEW: remove this method and force to use ctor(capacity, comparer) instead?
		{
			m_items = new Dictionary<string, JsonValue>(0, comparer ?? StringComparer.Ordinal);
		}

		public JsonObject(int capacity, IEqualityComparer<string>? comparer)
		{
			Contract.Positive(capacity);
			m_items = new Dictionary<string, JsonValue>(capacity, comparer ?? StringComparer.Ordinal);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use obj.Copy() to clone an object")]
		public JsonObject(JsonObject copy)
		{
			Contract.NotNull(copy);
			
			// we have to create a defensive copy
			m_items = new Dictionary<string, JsonValue>(copy.m_items, copy.Comparer);
			CheckInvariants();
		}

		/// <summary>Creates a new <see cref="JsonObject"/> that will wrap the specified items</summary>
		/// <param name="items">Pre-computed map of elements</param>
		/// <param name="readOnly">If the object is marked as read-only</param>
		/// <remarks>If <paramref name="readOnly"/> is <see langword="true"/> then all elements in <paramref name="items"/> MUST also be read-only!</remarks>
		internal JsonObject(Dictionary<string, JsonValue> items, bool readOnly)
		{
			Contract.Debug.Requires(items != null);
			m_items = items;
			m_readOnly = readOnly;
			CheckInvariants();
		}

		[Conditional("CHECK_INVARIANTS")]
		private void CheckInvariants()
		{
#if CHECK_INVARIANTS
			if (m_readOnly)
			{
				foreach (var kv in m_items)
				{
					if (!kv.Value.IsReadOnly) Contract.Fail($"Immutable JSON object cannot contain mutable children '{kv.Key}' of type {kv.Value.Type}");
				}
			}
#endif
		}

		internal static JsonObject CreateEmptyWithComparer(IEqualityComparer<string>? comparer) => new(new (comparer ?? StringComparer.Ordinal), readOnly: false);

		internal static JsonObject CreateEmptyWithComparer<TValue>(IEqualityComparer<string>? comparer, [JetBrains.Annotations.NoEnumeration] IEnumerable<KeyValuePair<string, TValue>>? items) => new(new (comparer ?? items switch
		{
			null => StringComparer.Ordinal,
			JsonObject obj => obj.m_items.Comparer,
			Dictionary<string, JsonValue> dic => dic.Comparer,
			ImmutableDictionary<string, JsonValue> imm => imm.KeyComparer,
			_ => StringComparer.Ordinal
		}), readOnly: false);

		/// <summary>Essayes d'extraire le KeyComparer d'un dictionnaire existant</summary>
		internal static IEqualityComparer<string>? ExtractKeyComparer<TValue>([JetBrains.Annotations.NoEnumeration] IEnumerable<KeyValuePair<string, TValue>> items)
		{
			//note: pour le cas où T == JsonValue, on check quand même si c'est un JsonObject!
			// ReSharper disable once SuspiciousTypeConversion.Global
			// ReSharper disable once ConstantNullCoalescingCondition
			return (items as JsonObject)?.Comparer ?? (items as Dictionary<string, TValue>)?.Comparer;
		}

		/// <summary>Freeze this object, once it has been initialized, by switching it to read-only mode.</summary>
		/// <remarks>Once "frozen", the operation cannot be reverted, and if additional mutation is required, a new copy of the object must be used.</remarks>
		public override JsonObject Freeze()
		{
			if (!m_readOnly)
			{ // at least one mutable children must be frozen as well!
				foreach (var value in m_items.Values)
				{
					value.Freeze();
				}
				m_readOnly = true;
			}

			CheckInvariants();
			return this;
		}

		internal JsonObject FreezeUnsafe()
		{
			if (m_items.Count == 0) return EmptyReadOnly;

			m_readOnly = true;
			CheckInvariants();
			return this;
		}

		/// <summary>Return a new immutable read-only version of this JSON object (and all of its children)</summary>
		/// <returns>The same object, if it is already immutable; otherwise, a deep copy marked as read-only.</returns>
		/// <remarks>A JSON object that is immutable is truly safe against any modification, including of any of its direct or indirect children.</remarks>
		public override JsonObject ToReadOnly()
		{
			if (m_readOnly)
			{
				CheckInvariants();
				return this;
			}

			var items = m_items;
			var map = new Dictionary<string, JsonValue>(items.Count, items.Comparer);
			foreach (var item in items)
			{
				var child = item.Value.Copy(deep: true, readOnly: true);
#if DEBUG
				Contract.Debug.Assert(child.IsReadOnly);
#endif
				map[item.Key] = child;
			}
			return new(map, readOnly: true);
		}

		/// <summary>Convert this JSON Object so that it, or any of its children that were previously read-only, can be mutated.</summary>
		/// <returns>The same instance if it is already fully mutable, OR a copy where any read-only Object or Array has been converted to allow mutations.</returns>
		/// <remarks>
		/// <para>Will return the same instance if it is already mutable, or a new deep copy with all children marked as mutable.</para>
		/// <para>This attempts to only copy what is necessary, and will not copy objects or arrays that are already mutable, or all other "value types" (strings, booleans, numbers, ...) that are always immutable.</para>
		/// </remarks>
		public override JsonObject ToMutable()
		{
			if (m_readOnly)
			{ // create a mutable copy
				return Copy();
			}

			// the top-level is mutable, but maybe it has read-only children?
			Dictionary<string, JsonValue>? copy = null;
			foreach (var (k, v) in m_items)
			{
				if (v is (JsonObject or JsonArray) && v.IsReadOnly)
				{
					copy ??= new (m_items.Count, m_items.Comparer);
					copy[k] = v.Copy();
				}
			}

			if (copy == null)
			{ // already mutable
				return this;
			}

			return new(copy, readOnly: false);
		}

		/// <summary>Return a new mutable copy of this JSON array (and all of its children)</summary>
		/// <returns>A deep copy of this array and its children.</returns>
		/// <remarks>
		/// <para>This will recursively copy all JSON objects or arrays present in the array, even if they are already mutable.</para>
		/// <para>The new instance can be freely modified without any effect on its parent. Likewise, if the parent is modified, it will not have any effect on the copy.</para>
		/// </remarks>
		public override JsonObject Copy()
		{
			var items = m_items;
			if (items.Count == 0) return new JsonObject();

			var map = new Dictionary<string, JsonValue>(items.Count, items.Comparer);
			// we want to make sure that any mutable children is copied as well
			foreach (var kvp in items)
			{
				map[kvp.Key] = kvp.Value.Copy();
			}

			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Create a copy of this object</summary>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the object, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		protected internal override JsonObject Copy(bool deep, bool readOnly) => Copy(this, deep, readOnly);

		/// <summary>Create a copy of a JSON object</summary>
		/// <param name="obj">Object to copy</param>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the object, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonObject Copy(JsonObject obj, bool deep = false, bool readOnly = false)
		{
			Contract.NotNull(obj);

			if (readOnly)
			{
				return obj.ToReadOnly();
			}

			if (deep)
			{
				return obj.Copy();
			} 
			
			// simply create a shallow copy of the top-level
			var items = obj.m_items;
			return new JsonObject(new Dictionary<string, JsonValue>(items, items.Comparer), readOnly: false);
		}

		#region Create...

		#region Mutable...

		/// <summary>Create a new empty JSON object</summary>
		/// <returns>JSON object of size 0, that can be modified.</returns>
		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonObject Create() => new(new Dictionary<string, JsonValue>(0, StringComparer.Ordinal), readOnly: false);

		/// <summary>Create a new JSON object with a single field</summary>
		/// <param name="key0">Name of the field</param>
		/// <param name="value0">Value of the field</param>
		/// <returns>JSON object of size 1, that can be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject Create(string key0, JsonValue? value0) => new(new Dictionary<string, JsonValue>(1, StringComparer.Ordinal)
		{
			[key0] = value0 ?? JsonNull.Null
		}, readOnly: false);

		/// <summary>Create a new JSON object with 2 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <returns>JSON object of size 2, that can be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1) => new(new Dictionary<string, JsonValue>(2, StringComparer.Ordinal)
		{
			{ key0, value0 ?? JsonNull.Null },
			{ key1, value1 ?? JsonNull.Null },
		}, readOnly: false);

		/// <summary>Create a new JSON object with 3 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <param name="key2">Name of the third field</param>
		/// <param name="value2">Value of the third field</param>
		/// <returns>JSON object of size 3, that can be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1, string key2, JsonValue? value2) => new(new Dictionary<string, JsonValue>(3, StringComparer.Ordinal)
		{
			{ key0, value0 ?? JsonNull.Null },
			{ key1, value1 ?? JsonNull.Null },
			{ key2, value2 ?? JsonNull.Null },
		}, readOnly: false);

		/// <summary>Create a new JSON object with 4 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <param name="key2">Name of the third field</param>
		/// <param name="value2">Value of the third field</param>
		/// <param name="key3">Name of the fourth field</param>
		/// <param name="value3">Value of the fourth field</param>
		/// <returns>JSON object of size 4, that can be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1, string key2, JsonValue? value2, string key3, JsonValue? value3) => new(new Dictionary<string, JsonValue>(4, StringComparer.Ordinal)
		{
			{ key0, value0 ?? JsonNull.Null },
			{ key1, value1 ?? JsonNull.Null },
			{ key2, value2 ?? JsonNull.Null },
			{ key3, value3 ?? JsonNull.Null },
		}, readOnly: false);

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(JsonObject items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).AddRange(items);
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(Dictionary<string, JsonValue> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).AddRange(items);
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(ImmutableDictionary<string, JsonValue> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).AddRange(items);
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer"></param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(ReadOnlySpan<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			return CreateEmptyWithComparer(comparer).AddRange(items);
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer"></param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(KeyValuePair<string, JsonValue>[] items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer).AddRange(items.AsSpan());
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer"></param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(IEnumerable<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddRange(items);
		}

		#endregion

		#region Immutable...

		/// <summary>Create a new empty read-only JSON object</summary>
		/// <returns>JSON object of size 0, that cannot be modified.</returns>
		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonObject CreateReadOnly() => EmptyReadOnly;
		
		/// <summary>Create a new immutable JSON object with a single field</summary>
		/// <param name="key0">Name of the field</param>
		/// <param name="value0">Value of the field</param>
		/// <returns>JSON object of size 1, that cannot be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject CreateReadOnly(string key0, JsonValue? value0) => new(new Dictionary<string, JsonValue>(1, StringComparer.Ordinal)
		{
			[key0] = (value0 ?? JsonNull.Null).ToReadOnly()
		}, readOnly: true);

		/// <summary>Create a new immutable JSON object with 2 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <returns>JSON object of size 2, that cannot be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject CreateReadOnly(string key0, JsonValue? value0, string key1, JsonValue? value1) => new(new Dictionary<string, JsonValue>(2, StringComparer.Ordinal)
		{
			{ key0, (value0 ?? JsonNull.Null).ToReadOnly() },
			{ key1, (value1 ?? JsonNull.Null).ToReadOnly() },
		}, readOnly: true);

		/// <summary>Create a new immutable JSON object with 3 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <param name="key2">Name of the third field</param>
		/// <param name="value2">Value of the third field</param>
		/// <returns>JSON object of size 3, that cannot be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject CreateReadOnly(string key0, JsonValue? value0, string key1, JsonValue? value1, string key2, JsonValue? value2) => new(new Dictionary<string, JsonValue>(3, StringComparer.Ordinal)
		{
			{ key0, (value0 ?? JsonNull.Null).ToReadOnly() },
			{ key1, (value1 ?? JsonNull.Null).ToReadOnly() },
			{ key2, (value2 ?? JsonNull.Null).ToReadOnly() },
		}, readOnly: true);

		/// <summary>Create a immutable new JSON object with 4 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <param name="key2">Name of the third field</param>
		/// <param name="value2">Value of the third field</param>
		/// <param name="key3">Name of the fourth field</param>
		/// <param name="value3">Value of the fourth field</param>
		/// <returns>JSON object of size 4, that cannot be modified.</returns>
		[System.Diagnostics.Contracts.Pure]
		public static JsonObject CreateReadOnly(string key0, JsonValue? value0, string key1, JsonValue? value1, string key2, JsonValue? value2, string key3, JsonValue? value3) => new(new Dictionary<string, JsonValue>(4, StringComparer.Ordinal)
		{
			{ key0, (value0 ?? JsonNull.Null).ToReadOnly() },
			{ key1, (value1 ?? JsonNull.Null).ToReadOnly() },
			{ key2, (value2 ?? JsonNull.Null).ToReadOnly() },
			{ key3, (value3 ?? JsonNull.Null).ToReadOnly() },
		}, readOnly: false);

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject CreateReadOnly(JsonObject items)
		{
			return CreateEmptyWithComparer(null, items).AddRangeReadOnly(items).FreezeUnsafe();
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject CreateReadOnly(Dictionary<string, JsonValue> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).AddRangeReadOnly(items).FreezeUnsafe();
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject CreateReadOnly(ImmutableDictionary<string, JsonValue> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).AddRangeReadOnly(items).FreezeUnsafe();
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer"></param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject CreateReadOnly(ReadOnlySpan<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			return CreateEmptyWithComparer(comparer).AddRangeReadOnly(items).FreezeUnsafe();
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer"></param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject CreateReadOnly(KeyValuePair<string, JsonValue>[] items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer).AddRangeReadOnly(items.AsSpan()).FreezeUnsafe();
		}

		/// <summary>Create a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer"></param>
		/// <returns>New JSON object with the same elements in <see cref="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject CreateReadOnly(IEnumerable<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddRangeReadOnly(items).FreezeUnsafe();
		}

		#endregion

		#endregion

		#region FromValues...

		public static JsonObject FromValues<TValue>(IEnumerable<KeyValuePair<string, TValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddValues<TValue>(items);
		}

		public static JsonObject FromValuesReadOnly<TValue>(IEnumerable<KeyValuePair<string, TValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddValuesReadOnly<TValue>(items).FreezeUnsafe();
		}

		public static JsonObject FromValues<TValue>(IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			comparer ??= ExtractKeyComparer(members) ?? StringComparer.Ordinal;

			var items = new Dictionary<string, JsonValue>(members.Count, comparer);
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Missing);
			}
			return new JsonObject(items, readOnly: false);
		}

		public static JsonObject FromValuesReadOnly<TValue>(IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			comparer ??= ExtractKeyComparer(members) ?? StringComparer.Ordinal;

			var items = new Dictionary<string, JsonValue>(members.Count, comparer);
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, (valueSelector(kvp.Value) ?? JsonNull.Missing).ToReadOnly());
			}
			return new JsonObject(items, readOnly: true);
		}

		public static JsonObject FromValues<TValue>(IDictionary<string, TValue> members, Func<TValue, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			comparer ??= ExtractKeyComparer(members) ?? StringComparer.Ordinal;

			var items = new Dictionary<string, JsonValue>(members.Count, comparer);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, JsonValue.FromValue(CrystalJsonDomWriter.Default, ref context, valueSelector(kvp.Value)));
			}
			return new JsonObject(items, readOnly: false);
		}

		public static JsonObject FromValues<TElement>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer ?? StringComparer.Ordinal);
			foreach (var item in source)
			{
				map.Add(keySelector(item), valueSelector(item) ?? JsonNull.Missing);
			}
			return new JsonObject(map, readOnly: false);
		}

		public static JsonObject FromValues<TElement, TValue>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer ?? StringComparer.Ordinal);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var item in source)
			{
				var child = FromValue<TValue>(CrystalJsonDomWriter.Default, ref context, valueSelector(item));
				map.Add(keySelector(item), child);
			}
			return new JsonObject(map, readOnly: false);
		}

		public static JsonObject FromValuesReadOnly<TElement, TValue>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer ?? StringComparer.Ordinal);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var item in source)
			{
				var child = FromValue<TValue>(CrystalJsonDomWriter.DefaultReadOnly, ref context, valueSelector(item));
				Contract.Debug.Assert(child.IsReadOnly);
				map.Add(keySelector(item), child);
			}
			return new JsonObject(map, readOnly: true);
		}

		#endregion

		#region FromObject...

		/// <summary>Transforme un objet CLR en un JsonObject</summary>
		/// <typeparam name="TValue">Type de l'objet à convertir</typeparam>
		/// <param name="value">Instance de l'objet à convertir</param>
		/// <returns>JsonObject correspondant, ou null si <paramref name="value"/> est null</returns>
		[JetBrains.Annotations.ContractAnnotation("value:notnull => notnull")]
		[return: NotNullIfNotNull(nameof(value))]
		public static JsonObject? FromObject<TValue>(TValue value)
		{
			//REVIEW: que faire si c'est null? Json.Net throw une ArgumentNullException dans ce cas, et ServiceStack ne gère pas de DOM de toutes manières...
			return CrystalJsonDomWriter.Default.ParseObject(value, typeof(TValue)).AsObject(required: false);
		}

		/// <summary>Transforme un objet CLR en un JsonObject</summary>
		/// <typeparam name="TValue">Type de l'objet à convertir</typeparam>
		/// <param name="value">Instance de l'objet à convertir</param>
		/// <returns>JsonObject correspondant, ou null si <paramref name="value"/> est null</returns>
		[JetBrains.Annotations.ContractAnnotation("value:notnull => notnull")]
		[return: NotNullIfNotNull(nameof(value))]
		public static JsonObject? FromObjectReadOnly<TValue>(TValue value)
		{
			//REVIEW: que faire si c'est null? Json.Net throw une ArgumentNullException dans ce cas, et ServiceStack ne gère pas de DOM de toutes manières...
			return CrystalJsonDomWriter.DefaultReadOnly.ParseObject(value, typeof(TValue)).AsObject(required: false);
		}

		[JetBrains.Annotations.ContractAnnotation("value:notnull => notnull")]
		[return: NotNullIfNotNull(nameof(value))]
		public static JsonObject? FromObject<TValue>(TValue value, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, typeof(TValue)).AsObject(required: false);
		}

		[JetBrains.Annotations.ContractAnnotation("value:notnull => notnull")]
		[return: NotNullIfNotNull(nameof(value))]
		public static JsonObject? FromObjectReadOnly<TValue>(TValue value, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject(value, typeof(TValue)).AsObject(required: false);
		}

		#endregion

		/// <summary>Convertit un dictionnaire en JsonObject, en convertissant chaque valeur du dictionnaire en JsonValue</summary>
		/// <returns>Ne pas utiliser cette méthode pour *construire* un JsonObject! Elle n'est à utiliser que pour s'interfacer avec une API qui utilise des dictionnaires, comme par exemple OWIN</returns>
		public static JsonObject CreateBoxed(IDictionary<string, object> members, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			var map = new Dictionary<string, JsonValue>(members.Count, comparer ?? ExtractKeyComparer(members) ?? StringComparer.Ordinal);
			foreach (var kvp in members)
			{
				map.Add(kvp.Key, FromValue(kvp.Value));
			}
			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Convertit un dictionnaire en JsonObject, en convertissant chaque valeur du dictionnaire en JsonValue</summary>
		/// <returns>Ne pas utiliser cette méthode pour *construire* un JsonObject! Elle n'est à utiliser que pour s'interfacer avec une API qui utilise des dictionnaires, comme par exemple OWIN</returns>
		public static JsonObject CreateBoxedReadOnly(IDictionary<string, object> members, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			var map = new Dictionary<string, JsonValue>(members.Count, comparer ?? ExtractKeyComparer(members) ?? StringComparer.Ordinal);
			foreach (var kvp in members)
			{
				map.Add(kvp.Key, FromValueReadOnly(kvp.Value));
			}
			return new JsonObject(map, readOnly: true);
		}
		
#if NET8_0_OR_GREATER
		[Obsolete]
#endif
		private static System.Runtime.Serialization.FormatterConverter? CachedFormatterConverter;

		/// <summary>Serialize an <see cref="Exception"/> into a JSON object</summary>
		/// <returns></returns>
		/// <remarks>
		/// The exception must implement <see cref="ISerializable"/>, and CANNOT contain cycles or self-references!
		/// The JSON object produced MAY NOT be deserializable back into the original exception type!
		/// </remarks>
		[System.Diagnostics.Contracts.Pure]
#if NET8_0_OR_GREATER
		[Obsolete("Formatter-based serialization is obsolete and should not be used.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public static JsonObject FromException(Exception ex, bool includeTypes = true)
		{
			Contract.NotNull(ex);
			if (ex is not ISerializable ser)
			{
				throw new JsonSerializationException($"Cannot serialize exception of type '{ex.GetType().FullName}' because it is not marked as Serializable.");
			}

			return FromISerializable(ser, includeTypes);
		}

		/// <summary>Serialize a type that implements <see cref="ISerializable"/> into a JSON object representation</summary>
		/// <remarks>
		/// The JSON object produced MAY NOT be deserializable back into the original exception type!
		/// </remarks>
		[System.Diagnostics.Contracts.Pure]
#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public static JsonObject FromISerializable(ISerializable value, bool includeTypes = true, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(value);

			settings ??= CrystalJsonSettings.Json;
			resolver ??= CrystalJson.DefaultResolver;

			var formatter = CachedFormatterConverter ??= new FormatterConverter();
			var info = new SerializationInfo(value.GetType(), formatter);
			var ctx = new StreamingContext(StreamingContextStates.Persistence);

			value.GetObjectData(info, ctx);

			var obj = new JsonObject();
			var it = info.GetEnumerator();
			{
				while (it.MoveNext())
				{
					object? x = it.Value;
					if (includeTypes)
					{ // round-trip mode: "NAME: [ TYPE, VALUE ]"
						var v = x is ISerializable ser
							? FromISerializable(ser, includeTypes: true, settings: settings, resolver: resolver)
							: FromValue(x, it.ObjectType, settings, resolver);
						// even if the value is null, we still have to provide the type!
						obj[it.Name] = JsonArray.Create(JsonString.Return(it.ObjectType), v);
					}
					else
					{ // compact mode: "NAME: VALUE"

						// since we don't care to be deserializable, we can ommit 'null' items
						if (x == null) continue;

						var v = x is ISerializable ser
							? FromISerializable(ser, includeTypes: false, settings: settings, resolver: resolver)
							: FromValue(x, settings, resolver);

						obj[it.Name] = v;
					}
				}
			}

			return obj;
		}

		#endregion

		public int Count => m_items.Count;

		ICollection<string> IDictionary<string, JsonValue>.Keys => m_items.Keys;

		IEnumerable<string> IReadOnlyDictionary<string, JsonValue>.Keys => m_items.Keys;

		public Dictionary<string, JsonValue>.KeyCollection Keys => m_items.Keys;

		ICollection<JsonValue> IDictionary<string, JsonValue>.Values => m_items.Values;

		IEnumerable<JsonValue> IReadOnlyDictionary<string, JsonValue>.Values => m_items.Values;

		public Dictionary<string, JsonValue>.ValueCollection Values => m_items.Values;

		public Dictionary<string, JsonValue>.Enumerator GetEnumerator() => m_items.GetEnumerator();

		IEnumerator<KeyValuePair<string, JsonValue>> IEnumerable<KeyValuePair<string, JsonValue>>.GetEnumerator() => m_items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => m_items.GetEnumerator();

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public override bool IsReadOnly => m_readOnly;

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public IEqualityComparer<string> Comparer => m_items.Comparer;

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		private static void FailObjectIsReadOnly() => throw new InvalidOperationException("Cannot mutate a read-only JSON object.");

		[EditorBrowsable(EditorBrowsableState.Always)]
		[System.Diagnostics.CodeAnalysis.AllowNull]
		public override JsonValue this[string key]
		{
			get
			{
				Contract.Debug.Requires(key != null);
				return m_items.TryGetValue(key, out var value) ? value : JsonNull.Missing;
			}
			set
			{
				if (m_readOnly) FailObjectIsReadOnly();
				Contract.Debug.Requires(key != null && !ReferenceEquals(this, value));
				m_items[key] = value ?? JsonNull.Null;
			}
		}

		[EditorBrowsable(EditorBrowsableState.Always)]
		[JetBrains.Annotations.ContractAnnotation("halt<=key:null; =>true,value:notnull; =>false,value:null")]
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			return m_items.TryGetValue(key, out value);
		}

		[EditorBrowsable(EditorBrowsableState.Always)]
		[JetBrains.Annotations.ContractAnnotation("halt<=key:null; =>true,array:notnull; =>false,array:null")]
		public bool TryGetArray(string key, [MaybeNullWhen(false)] out JsonArray array)
		{
			m_items.TryGetValue(key, out var value);
			array = value as JsonArray;
			return array != null;
		}

		[EditorBrowsable(EditorBrowsableState.Always)]
		[JetBrains.Annotations.ContractAnnotation("halt<=key:null; =>true,obj:notnull; =>false,obj:null")]
		public bool TryGetObject(string key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			Contract.NotNull(key);
			m_items.TryGetValue(key, out var value);
			obj = value as JsonObject;
			return obj != null;
		}

		[EditorBrowsable(EditorBrowsableState.Always)]
		public void Add(string key, JsonValue? value)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.Debug.Requires(key != null && !ReferenceEquals(this, value));
			m_items.Add(key, value ?? JsonNull.Null);
		}

		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryAdd(string key, JsonValue? value)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.Debug.Requires(key != null && !ReferenceEquals(this, value));
			return m_items.TryAdd(key, value ?? JsonNull.Null);
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void Add(KeyValuePair<string, JsonValue> item)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
			// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
			m_items.Add(item.Key, item.Value ?? JsonNull.Null);
		}

		#region AddRange...

		#region Mutable...

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, item.Value ?? JsonNull.Null);
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(KeyValuePair<string, JsonValue>[] items)
		{
			Contract.NotNull(items);
			return AddRange(items.AsSpan());
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(JsonObject items)
		{
			if (m_readOnly) FailObjectIsReadOnly();

			var other = items.m_items;
			if (other.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + other.Count));

			foreach (var item in other)
			{
				Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, item.Value ?? JsonNull.Null);
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(Dictionary<string, JsonValue> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Count));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, item.Value ?? JsonNull.Null);
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(ImmutableDictionary<string, JsonValue> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Count));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, item.Value ?? JsonNull.Null);
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();

			switch (items)
			{
				case JsonObject obj:
				{
					return AddRange(obj);
				}
				case Dictionary<string, JsonValue> dict:
				{
					return AddRange(dict);
				}
				case ImmutableDictionary<string, JsonValue> imm:
				{
					return AddRange(imm);
				}
				case KeyValuePair<string, JsonValue>[] arr:
				{
					return AddRange(arr.AsSpan());
				}
				case List<KeyValuePair<string, JsonValue>> list:
				{
					return AddRange(CollectionsMarshal.AsSpan(list));
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(unchecked(self.Count + count));
					}

					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, item.Value ?? JsonNull.Null);
					}

					return this;
				}
			}
		}

		#endregion

		#region Immutable...

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(KeyValuePair<string, JsonValue>[] items)
		{
			Contract.NotNull(items);
			return AddRangeReadOnly(items.AsSpan());
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(JsonObject items)
		{
			if (m_readOnly) FailObjectIsReadOnly();

			var other = items.m_items;
			if (other.Count != 0)
			{
				var self = m_items;
				self.EnsureCapacity(unchecked(self.Count + other.Count));

				foreach (var item in other)
				{
					Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
					// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
					self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
				}
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(Dictionary<string, JsonValue> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			if (items.Count == 0) return this;

			var self = m_items;

			self.EnsureCapacity(unchecked(self.Count + items.Count));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(ImmutableDictionary<string, JsonValue> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			if (items.Count == 0) return this;

			var self = m_items;

			self.EnsureCapacity(unchecked(self.Count + items.Count));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();

			switch (items)
			{
				case JsonObject obj:
				{
					return AddRangeReadOnly(obj);
				}
				case Dictionary<string, JsonValue> dict:
				{
					return AddRangeReadOnly(dict);
				}
				case ImmutableDictionary<string, JsonValue> imm:
				{
					return AddRangeReadOnly(imm);
				}
				case KeyValuePair<string, JsonValue>[] arr:
				{
					return AddRangeReadOnly(arr.AsSpan());
				}
				case List<KeyValuePair<string, JsonValue>> list:
				{
					return AddRangeReadOnly(CollectionsMarshal.AsSpan(list));
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(unchecked(self.Count + count));
					}

					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key != null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
					}

					return this;
				}
			}
		}

		#endregion

		#endregion

		#region AddValues [of T] ...

		#region Mutable...

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(ReadOnlySpan<KeyValuePair<string, TValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();

			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Length));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValue(kvp.Value));
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(KeyValuePair<string, TValue>[] items)
		{
			Contract.NotNull(items);
			return AddValues<TValue>(items.AsSpan());
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(Dictionary<string, TValue> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.NotNull(items);

			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValue(kvp.Value));
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(List<KeyValuePair<string, TValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.NotNull(items);

			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValue(kvp.Value));
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(IEnumerable<KeyValuePair<string, TValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.NotNull(items);

			switch (items)
			{
				case Dictionary<string, TValue> dict:
				{
					return AddValues<TValue>(dict);
				}
				case List<KeyValuePair<string, TValue>> list:
				{
					return AddValues<TValue>(CollectionsMarshal.AsSpan(list));
				}
				case KeyValuePair<string, TValue>[] arr:
				{
					return AddValues<TValue>(arr.AsSpan());
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(checked(this.Count + count));
					}

					foreach (var kvp in items)
					{
						self.Add(kvp.Key, FromValue(kvp.Value));
					}

					return this;
				}
			}
		}

		#endregion

		#region Immutable...

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(ReadOnlySpan<KeyValuePair<string, TValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();

			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Length));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValueReadOnly(kvp.Value));
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(KeyValuePair<string, TValue>[] items)
		{
			Contract.NotNull(items);
			return AddValuesReadOnly<TValue>(items.AsSpan());
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(Dictionary<string, TValue> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.NotNull(items);

			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValueReadOnly(kvp.Value));
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(List<KeyValuePair<string, TValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.NotNull(items);

			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValueReadOnly(kvp.Value));
			}

			return this;
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(IEnumerable<KeyValuePair<string, TValue>> items)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.NotNull(items);

			switch (items)
			{
				case Dictionary<string, TValue> dict:
				{
					return AddValuesReadOnly<TValue>(dict);
				}
				case List<KeyValuePair<string, TValue>> list:
				{
					return AddValuesReadOnly<TValue>(CollectionsMarshal.AsSpan(list));
				}
				case KeyValuePair<string, TValue>[] arr:
				{
					return AddValuesReadOnly<TValue>(arr.AsSpan());
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(checked(this.Count + count));
					}

					foreach (var kvp in items)
					{
						self.Add(kvp.Key, FromValueReadOnly(kvp.Value));
					}

					return this;
				}
			}
		}

		#endregion

		#endregion

		/// <summary>Removes the value with the specified key from this object.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool Remove(string key)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.Debug.Requires(key != null);
			return m_items.Remove(key);
		}

		/// <summary>Removes the value with the specified key from this object, and copies the element to the <paramref name="value" /> parameter.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <param name="value">The removed element.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public bool Remove(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.Debug.Requires(key != null);
			return m_items.Remove(key, out value);
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public bool Remove(KeyValuePair<string, JsonValue> keyValuePair)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			Contract.Debug.Requires(keyValuePair.Key != null);
			if (!m_items.TryGetValue(keyValuePair.Key, out var prev) || !prev.Equals(keyValuePair.Value))
			{
				return false;
			}
			return m_items.Remove(keyValuePair.Key);
		}

		[EditorBrowsable(EditorBrowsableState.Always)]
		public void Clear()
		{
			if (m_readOnly) FailObjectIsReadOnly();
			m_items.Clear();
		}

		/// <summary>Ensures that the dictionary can hold up to a specified number of entries without any further expansion of its backing storage.</summary>
		/// <param name="capacity">The number of entries.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="capacity" /> is less than 0.</exception>
		/// <returns>The current capacity of the <see cref="T:System.Collections.Generic.Dictionary`2" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public int EnsureCapacity(int capacity) => m_items.EnsureCapacity(capacity);

		/// <summary>Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries.</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void TrimExcess() => this.TrimExcess(this.Count);

		/// <summary>Sets the capacity of this dictionary to hold up a specified number of entries without any further expansion of its backing storage.</summary>
		/// <param name="capacity">The new capacity.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="capacity" /> is less than <see cref="Count" />.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void TrimExcess(int capacity) => m_items.TrimExcess(capacity);

		#region Public Properties...

		/// <summary>Type d'objet JSON</summary>
		public override JsonType Type => JsonType.Object;

		/// <summary>Indique s'il s'agit de la valeur par défaut du type ("vide")</summary>
		public override bool IsDefault => this.Count == 0;

		/// <summary>Indique si l'objet contient des valeurs</summary>
		public bool HasValues => this.Count > 0;

		/// <summary>Retourne la valeur de l'attribut "__class", ou null si absent (ou pas une chaine)</summary>
		public string? CustomClassName => Get<string>(JsonTokens.CustomClassAttribute);

		#endregion

		#region Getters...

		[JetBrains.Annotations.ContractAnnotation("required:true => notnull")]
		private TJson? InternalGet<TJson>(JsonType expectedType, string key, bool required)
			where TJson : JsonValue
		{
			if (!TryGetValue(key, out var value))
			{ // The property does not exist in this object
				if (required) JsonValueExtensions.FailFieldIsNullOrMissing(key);
				return null;
			}
			if (value.Type == JsonType.Null)
			{ // The property exists, but is null or missing
				if (required) JsonValueExtensions.FailFieldIsNullOrMissing(key);
				return null;
			}
			if (value.Type != expectedType)
			{ // The property exists, but is not of the expected type ??
				throw Error_ExistingKeyTypeMismatch(key, value, expectedType);
			}
			return (TJson)value;
		}

		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException Error_ExistingKeyTypeMismatch(string key, JsonValue value, JsonType expectedType) => new($"The specified key '{key}' exists, but is a {value.Type} instead of expected {expectedType}", nameof(key));

		/// <summary>Test if the object contains the <paramref name="key"/> property.</summary>
		/// <param name="key">Name of the property</param>
		/// <returns>Returns <see langword="true" /> if the entry is present; otherwise, <see langword="false" /></returns>
		/// <remarks>Please note that this will return <see langword="true" /> even if the property value is null. To treat <c>null</c> the same as missing, plase use <see cref="Has(string)"/> instead.</remarks>
		/// <example>
		/// { Foo: "..." }.Has("Foo") => true
		/// { Foo: ""    }.Has("Foo") => true  // empty string
		/// { Foo: null  }.Has("Foo") => true  // explicit null
		/// { Bar: ".."  }.Has("Foo") => false // not found
		/// </example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool ContainsKey(string key) => m_items.ContainsKey(key);

		bool ICollection<KeyValuePair<string, JsonValue>>.Contains(KeyValuePair<string, JsonValue> keyValuePair) => ((ICollection<KeyValuePair<string, JsonValue>>)m_items).Contains(keyValuePair);

		/// <summary>Test if the object contains the <paramref name="key"/> property, and that its value is not <c>null</c></summary>
		/// <param name="key">Name of the property</param>
		/// <returns>Retourns <see langword="true" /> if the entry is present and not <see cref="JsonNull.Null"/> or <see cref="JsonNull.Missing"/>.</returns>
		/// <example>
		/// { Foo: "..." }.Has("Foo") => true
		/// { Foo: ""    }.Has("Foo") => true  // empty string is not 'null'
		/// { Foo: null  }.Has("Foo") => false // found but explicit null
		/// { Bar: ".."  }.Has("Foo") => false // not found
		/// </example>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool Has(string key) => TryGetValue(key, out var value) && !value.IsNullOrMissing();

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
		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue? Get<TValue>(string key) => this[key].As<TValue>();

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
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("=> false, value:null")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGet<TValue>(string key, out TValue? value)
		{
			if (m_items.TryGetValue(key, out var item) && !item.IsNullOrMissing())
			{
				value = item.As<TValue>();
				return true;
			}
			value = default;
			return true;
		}

		/// <summary>Retourne la valeur d'une propriété de cet objet, avec une contrainte de présence optionnelle</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="required">Si true, une exception est lancée si la propriété n'existe pas où vaut null. Si false, retourne default(<typeparamref name="TValue"/>) si la propriété est manquante ou vaut explicitement null</param>
		/// <returns>
		/// Valeur de la propriété <paramref name="key"/> convertit en <typeparamref name="TValue"/>, ou default(<typeparamref name="TValue"/>) si la propriété contient null ou n'existe pas.
		/// Si <typeparamref name="TValue"/> est un ValueType qui n'est pas nullable, et que le champ est manquant (avec <paramref name="required"/> == false), alors c'est le "zéro" du type qui sera retourné (0, false, Guid.Empty, ...)
		/// Si par contre <typeparamref name="TValue"/> est un <see cref="Nullable{T}"/> alors c'est bien 'null' qui sera retourné dans ce cas.
		/// </returns>
		/// <example>
		/// obj.Get&lt;int&gt;"FieldThatExists", required: true) // returns the value of the field as an int
		/// obj.Get&lt;bool&gt;"FieldThatExists", required: true) // ALWAYS specify 'required' to make sure not to call the Get&lt;bool&gt; with defaultValue by mistake!
		/// obj.Get&lt;string&gt;"FieldThatIsMissing", required: true) // => throws
		/// obj.Get&lt;string&gt;"FieldThatIsNull", required: true) // => throws
		/// </example>
		/// <remarks> Cette méthode est équivalente à <code>obj[key].RequiredField(key).As&lt;T&gt;()</code> </remarks>
		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[JetBrains.Annotations.ContractAnnotation("required:true => notnull")]
		public TValue? Get<TValue>(string key, bool required)
		{
			var val = this[key];
			if (required) val = val.RequiredField(key);
			return val.As<TValue>();
		}

		/// <summary>Retourne la valeur d'une propriété de cet objet, ou une valeur par défaut si elle n'existe pas</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> convertit en <typeparamref name="TValue"/>, ou default(<typeparamref name="TValue"/>} si la propriété contient null ou n'existe pas.</returns>
		/// <remarks>Cette méthode est équivalente à <code>obj[key].As&lt;T&gt;(defaultValue, resolver)</code></remarks>
		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public TValue? Get<TValue>(string key, ICrystalJsonTypeResolver resolver)
		{
			return this[key].As<TValue>(resolver);
		}

		/// <summary>Retourne la valeur JSON d'une propriété de cet objet</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, <see cref="JsonNull.Null"/> si la propriété contient null, ou <see cref="JsonNull.Missing"/> si la propriété n'existe pas.</returns>
		/// <remarks>Si la valeur est un vrai nul (ie: default(objet)), alors JsonNull.Null est retourné à la place.</remarks>
		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetValue(string key) => this[key];

		/// <summary>Retourne la valeur JSON d'une propriété de cet objet</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="required"></param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, <see cref="JsonNull.Null"/> si la propriété contient null, ou <see cref="JsonNull.Missing"/> si la propriété n'existe pas.</returns>
		/// <remarks>Si la valeur est un vrai nul (ie: default(objet)), alors JsonNull.Null est retourné à la place.</remarks>
		[System.Diagnostics.Contracts.Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetValue(string key, bool required)
		{
			var val = this[key];
			return required ? val.RequiredField(key) : val;
		}

		/// <summary>Retourne la valeur JSON d'une propriété de cet objet, ou une valeur JSON par défaut</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="missingValue">Valeur par défaut retournée si l'objet ne contient cette propriété</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, <see cref="JsonNull.Null"/> si la propriété existe et contient null, ou <paramref name="missingValue"/> si la propriété n'existe pas.</returns>
		/// <remarks>Si la valeur est un vrai null (ie: default(object)), alors JsonNull.Null est retourné à la place.</remarks>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("halt<=key:null")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetValueOrDefault(string key, JsonValue? missingValue) => TryGetValue(key, out var value) ? value : (missingValue ?? JsonNull.Missing);

		/// <summary>Retourne la valeur d'une propriété de type JsonObject</summary>
		/// <param name="key">Nom de la propriété qui contient le sous-objet recherché</param>
		/// <param name="required">Si <b>true</b> et que le champ n'existe pas, ou contient null, une exception est lancée. Sinon, la méthode retourne null.</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, ou null si la propriété contient null ou n'existe pas et <paramref name="required"/> est <b>false</b>. Génère une exception si la propriété ne contient pas un object.</returns>
		/// <exception cref="ArgumentException">Si l'objet contient une propriété nommée <paramref name="key"/>, mais qui n'est ni un JsonObject, ni null.</exception>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("required:true => notnull")]
		public JsonObject? GetObject(string key, bool required = false) => InternalGet<JsonObject>(JsonType.Object, key, required);

		[MethodImpl(MethodImplOptions.AggressiveInlining), JetBrains.Annotations.ContractAnnotation("required:true => notnull")]
		public JsonObject? GetObjectPath(string path, bool required = false) => GetPath(path).AsObject(required);

		/// <summary>Retourne la valeur d'une propriété de type JsonArray</summary>
		/// <param name="key">Nom de la propriété qui contient l'array recherchée</param>
		/// <param name="required">Si <b>true</b> et que le champ n'existe pas, ou contient <b>null</b>, une exception est lancée. Sinon, la méthode retourne null.</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonArray, ou null si la propriété contient null ou n'existe pas et que <paramref name="required"/> est <b>false</b>. Génère une exception si la propriété ne contient pas une array.</returns>
		/// <exception cref="ArgumentException">Si l'objet contient une propriété nommée <paramref name="key"/>, mais qui n'est ni une JsonArray, ni null.</exception>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("required:true => notnull")]
		public JsonArray? GetArray(string key, bool required = false) => InternalGet<JsonArray>(JsonType.Array, key, required);

		[MethodImpl(MethodImplOptions.AggressiveInlining), JetBrains.Annotations.ContractAnnotation("required:true => notnull")]
		public JsonArray? GetArrayPath(string path, bool required = false) => GetPath(path).AsArray(required);

		/// <summary>Retourne un objet fils, en le créant (vide) au besoin</summary>
		/// <param name="path">Path vers le fils (peut inclure des '.')</param>
		/// <returns>Object correspondant, ou object vide</returns>
		/// <example>{ }.GetOrCreate("foo").Set("bar", 123) => { "foo": { "bar": 123 } }
		/// { }.GetOrCreate("foo.bar").Set("baz", 123) => { "foo": { "bar": { "baz": 123 } } }</example>
		/// <remarks>Si un parent n'existe pas, il est également créé</remarks>
		/// <exception cref="System.ArgumentNullException">Si <paramref name="path"/> est null (ou vide)</exception>
		/// <exception cref="System.ArgumentException">Si un des élément dans <paramref name="path"/> existe et n'est pas un objet</exception>
		public JsonObject GetOrCreateObject(string path)
		{
			Contract.NotNullOrEmpty(path);

			return (JsonObject) SetPathInternal(path, null, JsonType.Object);
		}

		/// <summary>Retourne un objet fils, en le créant (vide) au besoin</summary>
		/// <param name="path">Path vers le fils (peut inclure des '.')</param>
		/// <returns>Object correspondant, ou object vide</returns>
		/// <example>{ }.GetOrCreate("foo").Set("bar", 123) => { "foo": { "bar": 123 } }
		/// { }.GetOrCreate("foo.bar").Set("baz", 123) => { "foo": { "bar": { "baz": 123 } } }</example>
		/// <remarks>Si un parent n'existe pas, il est également créé</remarks>
		/// <exception cref="System.ArgumentNullException">Si key est null (ou vide)</exception>
		/// <exception cref="System.ArgumentException">Si un des élément dans key existe et n'est pas un objet</exception>
		public JsonArray GetOrCreateArray(string path)
		{
			Contract.NotNullOrEmpty(path);

			return (JsonArray) SetPathInternal(path, null, JsonType.Array);
		}

		/// <summary>Retourne une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à lire, au format "foo", "foo.bar" ou "foo[2].baz"</param>
		/// <returns>Valeur correspondante, ou <see cref="JsonNull.Missing"/> si au moins une des composantes du path n'est pas trouvée</returns>
		[System.Diagnostics.Contracts.Pure]
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
						return name != null ? current[name] : current;
					}
					case JPathToken.Identifier:
					{
						name = tokenizer.GetIdentifierName();
						break;
					}
					case JPathToken.ObjectAccess:
					case JPathToken.ArrayIndex:
					{
						if (name != null)
						{
							var child = current[name];
							if (child.IsNullOrMissing()) return JsonNull.Missing;
							current = child;
							name = null;
						}
						if (token == JPathToken.ArrayIndex)
						{
							int index = tokenizer.GetArrayIndex();
							var child = current[index];
							if (child.IsNullOrMissing()) return JsonNull.Null;
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

		/// <summary>Retourne une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à lire, au format "foo", "foo.bar" ou "foo[2].baz"</param>
		/// <param name="required">Si true et que le champ n'existe pas dans l'objet, une exception est générée</param>
		/// <returns>Valeur correspondante, ou <see cref="JsonNull.Missing"/> si au moins une des composantes du path n'est pas trouvée</returns>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("required:true => notnull")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue? GetPath<TValue>(string path, bool required = false)
		{
			var val = GetPath(path);
			return required ? val.RequiredPath(path).As<TValue>() : val.As<TValue>();
		}

		/// <summary>Retourne ou crée le fils d'un objet, qui doit lui-même être un objet</summary>
		/// <param name="current">Noeud courant (doit être un objet)</param>
		/// <param name="name">Nom du fils de <paramref name="current"/> qui devrait être un objet (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'objet s'il n'existait pas. Si false, retourne null</param>
		/// <returns>Valeur du fils, initialisée à un objet vide si manquante</returns>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonObject? GetOrCreateChildObject(JsonValue current, string? name, bool createIfMissing)
		{
			Contract.Debug.Requires(current != null && current.Type == JsonType.Object);

			JsonValue child;
			if (name != null)
			{
				child = current[name];
				if (child.IsNullOrMissing())
				{
					if (!createIfMissing)
					{
						return null;
					}
					child = new JsonObject();
					current[name] = child;
				}
				else if (child is not JsonObject)
				{
					throw ThrowHelper.InvalidOperationException($"The specified key '{name}' exists, but is of type {child.Type} instead of expected Object");
				}
			}
			else
			{
				if (current is not JsonObject)
				{
					throw ThrowHelper.InvalidOperationException($"Selected value was of type {current.Type} instead of expected Object");
				}
				child = current;
			}
			return (JsonObject) child;
		}

		/// <summary>Retourne ou crée le fils d'un objet, qui doit être une array</summary>
		/// <param name="current">Noeud courrant (doit être un objet)</param>
		/// <param name="name">Nom du fils de <paramref name="current"/> qui devrait être un objet (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'array si elle n'existait pas. Si false, retourne null</param>
		/// <returns>Valeur du fils, initialisée à une array vide si manquante</returns>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonArray? GetOrCreateChildArray(JsonValue current, string? name, bool createIfMissing)
		{
			Contract.Debug.Requires(current != null && current.Type == JsonType.Object);

			JsonValue child;
			if (name != null)
			{
				child = current[name];
				if (child.IsNullOrMissing())
				{
					if (!createIfMissing)
					{
						return null;
					}
					child = JsonArray.Create(); // we assume the intent is to modify it
					current[name] = child;
				}
				else if (child is not JsonArray)
				{
					throw ThrowHelper.InvalidOperationException($"The specified key '{name}' exists, but is of type {child.Type} instead of expected Array");
				}
			}
			else
			{
				if (current is not JsonArray) throw ThrowHelper.InvalidOperationException($"Selected value was of type {current.Type} instead of expected Array");
				child = current;
			}
			return (JsonArray) child;
		}

		/// <summary>Retourne ou crée une entrée d'une array, qui doit être un objet</summary>
		/// <param name="array">Noeud courrant (doit être une array)</param>
		/// <param name="index">Index de l'entrée dans <paramref name="array"/> qui devrait être un objet (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'objet s'il n'existait pas. Si false, retourne null</param>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonObject? GetOrCreateEntryObject(JsonArray array, int index, bool createIfMissing)
		{
			var child = index < array.Count ? array[index] : null;
			if (child.IsNullOrMissing())
			{
				if (!createIfMissing)
				{
					return null;
				}
				var empty = JsonObject.Create(); // we assume the intent is to modify it, so create a mutable object!
				array.Set(index, empty);
				return empty;
			}

			return child as JsonObject ?? throw ThrowHelper.InvalidOperationException($"Selected item at position {index} was of type {child.Type} instead of expected Object");
		}

		/// <summary>Retourne ou crée une entrée d'une array, qui doit être aussi une array</summary>
		/// <param name="array">Noeud courrant (doit être une array)</param>
		/// <param name="index">Index de l'entrée dans <paramref name="array"/> qui devrait être une array (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'array si elle n'existait pas. Si false, retourne null</param>
		[System.Diagnostics.Contracts.Pure, JetBrains.Annotations.ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonArray? GetOrCreateEntryArray(JsonArray array, int index, bool createIfMissing)
		{
			var child = index < array.Count ? array[index] : null;
			if (child.IsNullOrMissing())
			{
				if (!createIfMissing)
				{
					return null;
				}

				// we assume the intent is to modify it, so create a mutable array!
				var empty = JsonArray.Create();
				array.Set(index, empty);
				return empty;
			}

			if (child is not JsonArray arr)
			{
				throw ThrowHelper.InvalidOperationException($"Selected item at position {index} was of type {child.Type} instead of expected Array");
			}

			return arr;
		}

		/// <summary>Crée ou modifie une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à créer ou modifier.</param>
		/// <param name="value">Nouvelle valeur</param>
		public void SetPath(string path, JsonValue? value)
		{
			Contract.NotNullOrEmpty(path);

			value ??= JsonNull.Null;
			SetPathInternal(path, value, value.Type);
		}

		private JsonValue SetPathInternal(string path, JsonValue? valueToSet, JsonType expectedType)
		{
			//Console.WriteLine($"SetPath({path}, {valueToSet}, {expectedType})");
			JsonValue current = this;
			var tokenizer = new JPathTokenizer(path);
			int? index = null;
			string? name = null;
			while (true)
			{
				var token = tokenizer.ReadNext();
				//Console.WriteLine($"- {token}@{tokenizer.Offset} = '{tokenizer.GetSourceToken()}'; name={name}, index={index}, current = {current.Type} {current.ToJsonCompact()}, total = {this.ToJsonCompact()}");
				switch (token)
				{
					case JPathToken.Identifier:
					{ // "Foo"
						name = tokenizer.GetIdentifierName();
						index = null;
						break;
					}
					case JPathToken.ArrayIndex:
					{
						if (index.HasValue)
						{ // combo d'indexer: foo[1][2]..
							var array = GetOrCreateChildArray(current, name, createIfMissing: true)!;
							current = GetOrCreateEntryArray(array, index.Value, createIfMissing: true)!;
							name = null;
						}
						index = tokenizer.GetArrayIndex();
						Contract.Debug.Assert(current != null);
						break;
					}
					case JPathToken.ObjectAccess:
					{
						// "(current.)name>.<" ou

						if (index.HasValue)
						{
							JsonArray array = name == null
								? current.AsArray(required: true)!
								: GetOrCreateChildArray(current, name, createIfMissing: true)!;

							current = GetOrCreateEntryObject(array, index.Value, createIfMissing: true)!;
						}
						else
						{
							current = GetOrCreateChildObject(current, name, createIfMissing: true)!;
						}
						Contract.Debug.Assert(current != null);
						index = null;
						name = null;
						break;
					}
					case JPathToken.End:
					{ // "(current).(name)" ou "(current).(name)[index]"
						if (index.HasValue)
						{
							// current.name doit être une array
							JsonArray array = name == null
								? current.AsArray(required: true)!
								: GetOrCreateChildArray(current, name, createIfMissing: true)!;

							if (valueToSet != null)
							{ // set value
								array.Set(index.Value, valueToSet);
							}
							else if (expectedType == JsonType.Array)
							{ // empty array
								valueToSet = GetOrCreateEntryArray(array, index.Value, createIfMissing: true)!;
							}
							else
							{ // empty object
								Contract.Debug.Assert(expectedType == JsonType.Object);
								valueToSet = GetOrCreateEntryObject(array, index.Value, createIfMissing: true)!;
							}
							Contract.Debug.Assert(valueToSet.Type == expectedType);
							return valueToSet;
						}

						// current doit être un objet
						if (current is not JsonObject) throw ThrowHelper.InvalidOperationException("TODO: object expected");

						if (name == null) throw ThrowHelper.FormatException("TODO: missing identifier at end of JPath");

						// update
						if (valueToSet != null)
						{
							current[name] = valueToSet;
						}
						else if (expectedType == JsonType.Array)
						{ // empty array
							valueToSet = GetOrCreateChildArray(current, name, createIfMissing: true)!;
						}
						else
						{ // empty object
							Contract.Debug.Assert(expectedType == JsonType.Object);
							valueToSet = GetOrCreateChildObject(current, name, createIfMissing: true)!;
						}
						Contract.Debug.Assert(valueToSet.Type == expectedType);
						return valueToSet;
					}
					default:
					{
						throw ThrowHelper.FormatException($"Invalid JPath token {token} at {tokenizer.Offset}: '{path}'");
					}
				}
				//Console.WriteLine($"  => name={name}, index={index}, current = {current.ToJsonCompact()}, total = {this.ToJsonCompact()}");
			}
		}

		/// <summary>Crée ou modifie une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à supprimer.</param>
		/// <returns>True si la valeur existait. False si elle n'a pas été trouvée</returns>
		public bool RemovePath(string path)
		{
			Contract.NotNullOrEmpty(path);

			JsonValue? current = this;
			var tokenizer = new JPathTokenizer(path);
			int? index = null;
			string? name = null;
			while (true)
			{
				var token = tokenizer.ReadNext();
				switch (token)
				{
					case JPathToken.Identifier:
					{ // "Foo"
						name = tokenizer.GetIdentifierName();
						index = null;
						break;
					}
					case JPathToken.ArrayIndex:
					{
						if (index.HasValue)
						{ // combo d'indexer: foo[1][2]..
							var array = GetOrCreateChildArray(current, name, createIfMissing: false);
							if (array == null) return false;
							current = GetOrCreateEntryArray(array, index.Value, createIfMissing: false);
							if (current.IsNullOrMissing()) return false;
							name = null;
						}
						index = tokenizer.GetArrayIndex();
						break;
					}
					case JPathToken.ObjectAccess:
					{
						// "(current.)name>.<" ou

						Contract.Debug.Assert(name != null);

						if (index.HasValue)
						{
							var array = GetOrCreateChildArray(current, name, createIfMissing: false);
							if (array == null) return false;
							current = GetOrCreateEntryObject(array, index.Value, createIfMissing: false);
							index = null;
						}
						else
						{
							current = GetOrCreateChildObject(current, name, createIfMissing: false);
						}
						if (current.IsNullOrMissing()) return false;
						name = null;
						break;
					}
					case JPathToken.End:
					{ // "(current).(name)" ou "(current).(name)[index]"
						if (index.HasValue)
						{
							// current.name doit être une array
							JsonArray? array;
							if (name == null)
							{
								array = current.AsArray(required: true)!;
							}
							else
							{
								array = GetOrCreateChildArray(current, name, createIfMissing: false);
								if (array == null) return false;
							}
							//TODO: set to null? removeAt?
							array.RemoveAt(index.Value);
							return true;
						}

						// current doit être un objet
						if (current is not JsonObject) throw ThrowHelper.InvalidOperationException("TODO: object expected");

						if (name == null) throw ThrowHelper.FormatException("TODO: missing identifier at end of JPath");

						// update
						return ((JsonObject) current).Remove(name);
					}
					default:
					{
						throw ThrowHelper.FormatException($"Invalid JPath token {token} at {tokenizer.Offset}: '{path}'");
					}
				}
			}
		}

		#endregion

		#region Setters...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject Set<TValue>(string key, TValue? value)
		{
			m_items[key] = FromValue<TValue>(value);
			return this;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject Set(string key, JsonValue? value)
		{
			m_items[key] = value ?? JsonNull.Null;
			return this;
		}


		/// <summary>Ajoute l'attribut "_class" avec l'id résolvé du type</summary>
		/// <typeparam name="TContainer">Type à résolver</typeparam>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		public JsonObject SetClassId<TContainer>(ICrystalJsonTypeResolver? resolver = null)
		{
			return SetClassId(typeof(TContainer), resolver);
		}

		/// <summary>Ajoute l'attribut "_class" avec l'id résolvé du type</summary>
		/// <param name="type">Type à résolver</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		public JsonObject SetClassId(Type type, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(type);

			var typeDef = (resolver ?? CrystalJson.DefaultResolver).ResolveJsonType(type) ?? throw CrystalJson.Errors.Serialization_CouldNotResolveTypeDefinition(type);
			this.ClassId = typeDef.ClassId;
			return this;
		}

		public string? ClassId
		{
			get => this[JsonTokens.CustomClassAttribute].ToStringOrDefault();
			set
			{
				if (string.IsNullOrEmpty(value))
				{
					Remove(JsonTokens.CustomClassAttribute);
				}
				else
				{
					this[JsonTokens.CustomClassAttribute] = value;
				}
			}
		}

		#endregion

		#region Merging...

		/// <summary>Copie les champs d'un autre objet dans l'objet courrant</summary>
		/// <param name="other">Autre objet JSON dont les champs vont être copié dans l'objet courrant. Tout champ déjà existant sera écrasé.</param>
		/// <param name="deepCopy">Si true, clone tous les champs de <paramref name="other"/> avant de le copier. Sinon, ils sont copiés par référence.</param>
		public void MergeWith(JsonObject other, bool deepCopy = false)
		{
			Merge(this, other, deepCopy);
		}

		public static JsonObject Merge(JsonObject parent, JsonObject? other, bool deepCopy = false)
		{
			Contract.NotNull(parent);

			if (other is not null && other.Count > 0)
			{
				// recursively merge all properties:
				// - copy the items from 'other', optionally merging them if they already exist in 'parent'
				// - Mergin properties will:
				//   - Overwrite for "immutable types" (string, bool, int, ...)
				//   - Merge for Object
				//   - Union for Array (?)

				foreach (var kvp in other)
				{
					if (!parent.TryGetValue(kvp.Key, out var mine))
					{
						// note: ignore "Missing", but not explicit "Null"
						if (!kvp.Value.IsMissing())
						{
							parent[kvp.Key] = deepCopy ? kvp.Value.Copy() : kvp.Value;
						}
						continue;
					}

					// Any "Missing" values will be treated as if the property has been removed
					if (kvp.Value.IsMissing())
					{
						parent.Remove(kvp.Key);
						continue;
					}

					switch (mine.Type)
					{
						case JsonType.String:
						case JsonType.Number:
						case JsonType.Boolean:
						case JsonType.DateTime:
						{ // overwrite
							parent[kvp.Key] = deepCopy ? kvp.Value.Copy() : kvp.Value;
							break;
						}


						case JsonType.Object:
						{ // merge
							if (kvp.Value.IsNull)
							{
								parent[kvp.Key] = JsonNull.Null;
								break;
							}

							if (kvp.Value is not JsonObject obj)
							{ // we only support merging between two objects
								throw ThrowHelper.InvalidOperationException($"Cannot merge a JSON '{kvp.Value.Type}' into an Object for key '{kvp.Key}'");
							}

							((JsonObject) mine).MergeWith(obj, deepCopy);
							break;
						}

						case JsonType.Null:
						{
							break;
						}

						case JsonType.Array:
						{ // union
							if (kvp.Value.IsNull)
							{
								parent[kvp.Key] = JsonNull.Null;
								break;
							}
							if (kvp.Value is not JsonArray arr)
							{ // we only support merging between two arrays
								throw ThrowHelper.InvalidOperationException($"Cannot merge a JSON '{kvp.Value.Type}' into an Array for key '{kvp.Key}'");
							}
							((JsonArray) mine).MergeWith(arr, deepCopy);
							break;
						}

						default:
						{
							throw ThrowHelper.InvalidOperationException($"Doesn't know how to merge JSON values of type {mine.Type} with type {kvp.Value.Type} for key '{kvp.Key}'");
						}
					}
				}
			}
			return parent;
		}

		#endregion

		#region Projection...

		/// <summary>Génère un Picker en cache, capable d'extraire une liste de champs d'objet JSON</summary>
		public static Func<JsonObject, JsonObject> CreatePicker(ReadOnlySpan<string> fields, bool removeFromSource = false)
		{
			var projections = CheckProjectionFields(fields, removeFromSource);
			return (obj) => Project(obj, projections);
		}

		/// <summary>Génère un Picker en cache, capable d'extraire une liste de champs d'objet JSON</summary>
		public static Func<JsonObject, JsonObject> CreatePicker(IEnumerable<string> fields, bool keepMissing, bool removeFromSource = false)
		{
			var projections = CheckProjectionFields(fields as string[] ?? fields.ToArray(), keepMissing);
			return (obj) => Project(obj, projections, removeFromSource);
		}

		/// <summary>Génère un Picker en cache, capable d'extraire une liste de champs d'objet JSON</summary>
		public static Func<JsonObject, JsonObject> CreatePicker(IDictionary<string, JsonValue?> defaults, bool removeFromSource = false)
		{
			var projections = CheckProjectionDefaults(defaults);
			return (obj) => Project(obj, projections, removeFromSource);
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="fields">Liste des noms des champs à conserver</param>
		/// <param name="keepMissing">Si false, les champs projetés qui n'existent pas dans l'objet source ne seront pas présent dans le résultat. Si true, les champs seront présents dans le résultat avec une valeur à 'null'</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="fields"/></returns>
		public JsonObject Pick(ReadOnlySpan<string> fields, bool keepMissing = false)
		{
			return Project(this, CheckProjectionFields(fields, keepMissing));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="fields">Liste des noms des champs à conserver</param>
		/// <param name="keepMissing">Si false, les champs projetés qui n'existent pas dans l'objet source ne seront pas présent dans le résultat. Si true, les champs seront présents dans le résultat avec une valeur à 'null'</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="fields"/></returns>
		public JsonObject Pick(string[] fields, bool keepMissing = false)
		{
			return Project(this, CheckProjectionFields(fields, keepMissing));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="fields">Liste des noms des champs à conserver</param>
		/// <param name="keepMissing">Si false, les champs projetés qui n'existent pas dans l'objet source ne seront pas présent dans le résultat. Si true, les champs seront présents dans le résultat avec une valeur à 'null'</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="fields"/></returns>
		public JsonObject Pick(IEnumerable<string> fields, bool keepMissing = false)
		{
			return Project(this, CheckProjectionFields(fields as string[] ?? fields.ToArray(), keepMissing));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="defaults">Liste des des champs à conserver, avec une éventuelle valeur par défaut</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="defaults"/></returns>
		public JsonObject PickFrom(IDictionary<string, JsonValue?> defaults)
		{
			return Project(this, CheckProjectionDefaults(defaults));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="defaults">Liste des des champs à conserver, avec une éventuelle valeur par défaut</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="defaults"/></returns>
		public JsonObject PickFrom(object defaults)
		{
			return Project(this, CheckProjectionDefaults(defaults));
		}

		/// <summary>Vérifie que la liste de champs de projection ne contient pas de null, empty ou doublons</summary>
		/// <param name="keys">Liste de nom de champs à projeter</param>
		/// <param name="keepMissing"></param>
		[JetBrains.Annotations.ContractAnnotation("keys:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionFields(ReadOnlySpan<string> keys, bool keepMissing)
		{
			var res = new KeyValuePair<string, JsonValue?>[keys.Length];
			var set = new HashSet<string>();
			int p = 0;

			foreach (var key in keys)
			{
				if (string.IsNullOrEmpty(key))
				{
					throw ThrowHelper.InvalidOperationException($"Cannot project empty or null field name: [{string.Join(", ", keys.ToArray())}]");
				}
				set.Add(key);
				res[p++] = new KeyValuePair<string, JsonValue?>(key, keepMissing ? JsonNull.Missing : null);
			}
			if (set.Count != keys.Length)
			{
				throw ThrowHelper.InvalidOperationException($"Cannot project duplicate field name: [{string.Join(", ", keys.ToArray())}]");
			}

			return res;
		}

		/// <summary>Vérifie que la liste de champs de projection ne contient pas de null, empty ou doublons</summary>
		/// <param name="defaults">Liste des clés à projeter, avec leur valeur par défaut</param>
		/// <remarks>Si un champ est manquant dans l'objet source, la valeur par défaut est utilisée, sauf si elle est égale à null.</remarks>
		[JetBrains.Annotations.ContractAnnotation("defaults:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionDefaults(IDictionary<string, JsonValue?> defaults)
		{
			Contract.NotNull(defaults);

			var res = new KeyValuePair<string, JsonValue?>[defaults.Count];
			var set = new HashSet<string>();
			int p = 0;

			foreach(var kvp in defaults)
			{
				if (string.IsNullOrEmpty(kvp.Key))
				{
					ThrowHelper.ThrowInvalidOperationException($"Cannot project empty or null field name: [{string.Join(", ", defaults.Select(x => x.Key))}]");
				}

				set.Add(kvp.Key);
				res[p++] = kvp;
			}

			if (set.Count != defaults.Count)
			{
				ThrowHelper.ThrowInvalidOperationException($"Cannot project duplicate field name: [{string.Join(", ", defaults.Select(x => x.Key))}]");
			}

			return res;
		}

		[JetBrains.Annotations.ContractAnnotation("defaults:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionDefaults(object defaults)
		{
			Contract.NotNull(defaults);

			var obj = FromObjectReadOnly(defaults);
			Contract.Debug.Assert(obj != null);
			//note: garantit sans doublons et sans clés vides
			return obj.ToArray()!;
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="item">Objet source</param>
		/// <param name="defaults">Liste des propriétés à conserver, avec leur valeur par défaut si elle n'existe pas dans la source</param>
		/// <param name="removeFromSource">Si true, retire les champs sélectionnés de <paramref name="item"/>. Si false, ils sont copiés dans le résultat</param>
		/// <returns>Nouvel objet qui ne contient que les champs de <paramref name="item"/> présents dans <paramref name="defaults"/></returns>
		/// <remarks>{ A: 1, C: false }.Project({ A: 0, B: 42, C: true}) => { A: 1, B: 42, C: false }</remarks>
		internal static JsonObject Project(JsonObject item, KeyValuePair<string, JsonValue?>[] defaults, bool removeFromSource = false)
		{
			Contract.Debug.Requires(item != null && defaults != null);

			var obj = new JsonObject(defaults.Length, item.Comparer);
			foreach (var prop in defaults)
			{
				if (item.TryGetValue(prop.Key, out var value))
				{
					obj[prop.Key] = value;
					if (removeFromSource)
					{
						item.Remove(prop.Key);
					}
				}
				else if (prop.Value != null)
				{
					obj[prop.Key] = prop.Value;
				}
			}
			return obj;
		}

		#endregion

		#region Filtering...

		/// <summary>Retourne un nouvel objet qui ne contient que les champs d'un objet source qui passent le filtre</summary>
		/// <param name="value">Objet source à filtrer</param>
		/// <param name="filter">Test appliqué sur le nom de chaque champ de <paramref name="value"/>. Les champs qui passent le filtre ne sont pas copiés dans le résultat.</param>
		/// <param name="deepCopy">Si true, fait une copie complète des champs conservés. Si false, copie la référence.</param>
		/// <returns>Nouvel objet filtré</returns>
		/// <remarks>Si aucun champ ne passe le filtre, un nouvel objet vide est retourné.</remarks>
		internal static JsonObject Without(JsonObject value, Func<string, bool> filter, bool deepCopy)
		{
			Contract.Debug.Requires(value != null && filter != null);

			// comme on ne peut pas savoir a l'avance combien de champs vont matcher, on fait quand même une copie de l'objet, qu'on drop si aucun champ n'a été modifié (le GC s'en occupera)
			// on espère que si quelqu'un appelle cette méthode, c'est que la probabilité d'au moins un match est élevée (et donc si ça match, on aurait du allouer l'objet de toute manière)

			var obj = new JsonObject(value.Count, value.Comparer);
			foreach(var item in value)
			{
				if (!filter(item.Key))
				{
					obj[item.Key] = deepCopy ? item.Value.Copy() :  item.Value;
				}
			}
			return obj;
		}

		/// <summary>Retourne un nouvel objet qui ne contient que les champs d'un objet source qui passent le filtre</summary>
		/// <param name="value">Objet source à filtrer</param>
		/// <param name="filtered">Test appliqué sur le nom de chaque champ de <paramref name="value"/>. Les champs qui passent le filtre ne sont pas copiés dans le résultat.</param>
		/// <param name="deepCopy">Si true, fait une copie complète des champs conservés. Si false, copie la référence.</param>
		/// <returns>Nouvel objet filtré</returns>
		/// <remarks>Si aucun champ ne passe le filtre, un nouvel objet vide est retourné.</remarks>
		internal static JsonObject Without(JsonObject value, HashSet<string> filtered, bool deepCopy)
		{
			Contract.Debug.Requires(value != null && filtered != null);

			// comme on ne peut pas savoir a l'avance combien de champs vont matcher, on fait quand même une copie de l'objet, qu'on drop si aucun champ n'a été modifié (le GC s'en occupera)
			// on espère que si quelqu'un appelle cette méthode, c'est que la probabilité d'au moins un match est élevée (et donc si ça match, on aurait du allouer l'objet de toute manière)

			var obj = new JsonObject(value.Count, value.Comparer);
			foreach (var item in value)
			{
				if (!filtered.Contains(item.Key))
				{
					obj[item.Key] = deepCopy ? item.Value.Copy() : item.Value;
				}
			}
			return obj;
		}

		/// <summary>Retourne une copie d'un objet sans un champ spécifique, s'il existe dans la source</summary>
		/// <param name="value">Objet qui contient éventuellement le champ <paramref name="field"/></param>
		/// <param name="field">Nom du champ à supprimer s'il existe</param>
		/// <param name="deepCopy">Si true, copie également les fils de cet objet</param>
		/// <returns>Nouvel objet sans le champ <paramref name="field"/>.</returns>
		internal static JsonObject Without(JsonObject value, string field, bool deepCopy)
		{
			Contract.Debug.Requires(value != null && field != null);

			//TODO: actuellement, on risque de faire une deepCopy du champ qui sera supprimé ensuite!
			var obj = JsonObject.Copy(value, deepCopy, readOnly: false);
			obj.Remove(field);
			return obj;
		}

		/// <summary>Retourne une copie de cet objet, à l'exception du champ spécifié</summary>
		/// <param name="filter">Nom du champ</param>
		/// <param name="deepCopy">Si true, effectue une copie complète de l'objet et de ses fils (récursivement). Sinon, ne copie que l'objet top-level.</param>
		/// <returns>Nouvel objet contenant les mêmes champs que, sauf <paramref name="filter"/>.</returns>
		public JsonObject Without(Func<string, bool> filter, bool deepCopy = false)
		{
			Contract.NotNull(filter);
			return Without(this, filter, deepCopy);
		}

		/// <summary>Retourne une copie de cet objet, à l'exception du champ spécifié</summary>
		/// <param name="fieldToRemove">Nom du champ</param>
		/// <param name="deepCopy">Si true, effectue une copie complète de l'objet et de ses fils (récursivement). Sinon, ne copie que l'objet top-level.</param>
		/// <returns>Nouvel objet contenant les mêmes champs que, sauf <paramref name="fieldToRemove"/>.</returns>
		public JsonObject Without(string fieldToRemove, bool deepCopy = false)
		{
			Contract.NotNullOrEmpty(fieldToRemove);
			return Without(this, fieldToRemove, deepCopy);
		}

		/// <summary>Supprime un champ de l'objet</summary>
		/// <param name="fieldToRemove">Nom du champ</param>
		/// <returns>Le même objet (éventuellement modifié)</returns>
		/// <remarks>Cette méthode est un alias sur <see cref="Remove(string)"/>, utilisable en mode Fluent</remarks>
		public JsonObject Erase(string fieldToRemove)
		{
			Contract.NotNullOrEmpty(fieldToRemove);
			this.Remove(fieldToRemove);
			return this;
		}

		#endregion

		#region Sorting...

		private static bool TrySortValue(JsonValue item, IComparer<string> comparer, [MaybeNullWhen(false)] out JsonValue result)
		{
			result = null!;

			switch (item)
			{
				case JsonObject obj:
				{
					if (TrySortByKeys(obj.m_items, comparer, out var subItems))
					{
						result = new JsonObject(subItems, obj.m_readOnly);
						return true;
					}

					return false;
				}
				case JsonArray arr:
				{
					// only allocate the buffer if at least one children has changed
					JsonValue[]? items = null;
					for (int i = 0; i < arr.Count; i++)
					{
						if (TrySortValue(arr[i], comparer, out var val))
						{
							(items ??= arr.ToArray())[i] = val;
						}
					}

					if (items != null)
					{ // at least one change
						result = new JsonArray(items, items.Length, arr.IsReadOnly);
						return true;
					}

					return false;
				}
				default:
				{
					return false;
				}
			}
		}

		/// <summary>Tri les clés d'un dictionnaire, en utilisant un comparer spécifique</summary>
		/// <param name="items">Dictionnaire contenant les items à trier</param>
		/// <param name="comparer">Comparer à utiliser</param>
		/// <param name="result">Dictionnaire dont les clés ont été insérées dans le bon ordre</param>
		private static bool TrySortByKeys(Dictionary<string, JsonValue> items, IComparer<string> comparer, [MaybeNullWhen(false)] out Dictionary<string, JsonValue> result)
		{
			//ATTENTION: cet algo se base sur le fait qu'actuellement (.NET 4.0 / 4.5) un Dictionary<K,V> conserve l'ordre d'insertion des clés, tant que personne ne supprime de clés.
			// => si jamais cela n'est plus vrai dans une nouvelle version de .NET, il faudra trouver une nouvelle méthode!

			Contract.Debug.Requires(items != null && comparer != null);
			result = null!;

			if (items.Count == 0)
			{ // pas besoin de trier
				return false;
			}

			//TODO: optimizer le cas Count == 1?

			bool changed = false;

			// capture l'état de l'objet
			var keys = new string[items.Count];
			var values = new JsonValue[items.Count];
			items.Keys.CopyTo(keys, 0);
			items.Values.CopyTo(values, 0);

			// il faut aussi trier les sous-éléments de cet objet
			for (int i = 0; i < values.Length; i++)
			{
				if (TrySortValue(values[i], comparer, out var val))
				{
					values[i] = val;
					changed = true;
				}
			}

			// tri des clés

			var indexes = new int[keys.Length];
			for (int i = 0; i < indexes.Length; i++) indexes[i] = i;
			Array.Sort(keys, indexes, comparer);

			if (!changed)
			{
				// Si toutes les clés étaient déjà dans le bon ordre, indexes var rester trié [0, 1, 2, ..., N-1].
				// Dans ce cas, on peut éviter de modifier cette objet.
				for (int i = 0; i < indexes.Length; i++)
				{
					if (indexes[i] != i)
					{ // il y a eu au moins une modification!
						changed = true;
						break;
					}
				}
			}

			if (changed)
			{ // aucune modification n'a été faite dans la sous-branche correspondant à cet objet
				// génère la nouvelle version de cet objet
				result = new Dictionary<string, JsonValue>(keys.Length, items.Comparer);
				for (int i = 0; i < keys.Length; i++)
				{
					result[keys[i]] = values[indexes[i]];
				}
				return true;
			}

			return false;
		}

		/// <summary>Tri les clés de ce dictionnaire dans un ordre spécifique</summary>
		/// <remarks>L'instance est modifiée si les clés n'étaient pas dans le bon ordre</remarks>
		public void SortKeys(IComparer<string>? comparer = null)
		{
			if (m_readOnly) FailObjectIsReadOnly();
			if (TrySortByKeys(m_items, comparer ?? StringComparer.Ordinal, out var items))
			{
				m_items.Clear();
				foreach (var kvp in items)
				{
					m_items[kvp.Key] = kvp.Value;
				}
			}
		}

		/// <summary>Retourne un nouveau document JSON, identique au premier, mais avec les clés triées suivant un ordre spécifique</summary>
		/// <param name="map">Object JSON source. Cet objet n'est pas modifié.</param>
		/// <param name="comparer">Comparer à utiliser (Ordinal par défaut)</param>
		/// <returns>Copie (non deep) de <paramref name="map"/> dont les clés sont triées selon <paramref name="comparer"/></returns>
		public static JsonObject OrderedByKeys(JsonObject map, IComparer<string>? comparer = null)
		{
			Contract.NotNull(map);

			if (TrySortByKeys(map.m_items, comparer ?? StringComparer.Ordinal, out var items))
			{
				return new JsonObject(items, map.m_readOnly);
			}

			//TODO: to copy or not to copy?
			return map;
		}

		/// <summary>Retourne un nouvel objet, identique à celui-ci, mais avec les clés dans un ordre spécifique</summary>
		public JsonObject OrderedByKeys(IComparer<string>? comparer = null)
		{
			return OrderedByKeys(this, comparer);
		}

		#endregion

		#region Conversion...

		internal override bool IsSmallValue()
		{
			const int LARGE_OBJECT = 5;
			if (m_items.Count >= LARGE_OBJECT)
			{
				return false;
			}

			foreach(var v in m_items.Values)
			{
				if (v.IsSmallValue())
				{
					return false;
				}
			}

			return true;
		}

		internal override bool IsInlinable() => false;

		private string GetMutabilityDebugLiteral() => m_readOnly ? " ReadOnly" : "";

		internal override string GetCompactRepresentation(int depth)
		{
			const int MAX_ITEMS = 4;

			if (m_items.Count == 0) return "{ }"; // empty

			// On va dumper jusqu'à 4 champs (ce qui couvre la majorité des "petits" objets
			// Si la valeur d'un field est "small" elle est dumpée intégralement, sinon elle est remplacer par des '...'

			var sb = new StringBuilder("{ ");
			int i = 0;
			foreach(var kv in m_items)
			{
				if (i >= MAX_ITEMS) { sb.Append($", /* \u2026 {(m_items.Count - MAX_ITEMS):N0} more */"); break; }
				if (i > 0) sb.Append(", ");

				sb.Append(kv.Key).Append(": ");
				if (depth == 0 || kv.Value.IsSmallValue())
				{ 
					sb.Append(kv.Value.GetCompactRepresentation(depth + 1));
				}
				else
				{
					switch (kv.Value.Type)
					{
						case JsonType.Object: sb.Append("{\u2026}"); break;
						case JsonType.Array: sb.Append("[\u2026]"); break;
						case JsonType.String: sb.Append("\"\u2026\""); break;
						default: sb.Append('\u2026'); break;
					}
				}
				i++;
			}
			sb.Append(" }");
			return sb.ToString();
		}

		public override object? ToObject()
		{
			return CrystalJsonParser.DeserializeCustomClassOrStruct(this, typeof(object), CrystalJson.DefaultResolver);
		}

		public override TValue? Bind<TValue>(ICrystalJsonTypeResolver? resolver = null) where TValue : default
		{
			var res = (resolver ?? CrystalJson.DefaultResolver).BindJsonObject(typeof(TValue), this);
			return default(TValue) == null && res == null ? JsonNull.Default<TValue>() : (TValue?) res;
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			return (resolver ?? CrystalJson.DefaultResolver).BindJsonObject(type, this);
		}

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			foreach (var item in this)
			{
				writer.WriteField(item.Key, item.Value);
			}
			writer.EndObject(state);
		}

		#endregion

		#region IEquatable<...>

		public override bool Equals(JsonValue? other) => other is JsonObject obj && Equals(obj);

		public bool Equals(JsonObject? other)
		{
			if (other is null || other.Count != this.Count)
			{
				return false;
			}

			foreach (var kvp in this)
			{
				if (!other.TryGetValue(kvp.Key, out var o) || !o.Equals(kvp.Value))
				{
					return false;
				}
			}
			return true;
		}

		public bool Equals(JsonObject? other, IEqualityComparer<JsonValue>? comparer)
		{
			if (other is null || other.Count != this.Count)
			{
				return false;
			}
			comparer ??= JsonValueComparer.Default;
			foreach (var kvp in this)
			{
				if (!other.TryGetValue(kvp.Key, out var o) || !comparer.Equals(o, kvp.Value))
				{
					return false;
				}
			}
			return true;
		}

		public override int GetHashCode()
		{
			// le hashcode de l'objet ne doit pas changer meme s'il est modifié (sinon on casse les hashtables!)
			return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

			//TODO: si on jour on gère les Read-Only dictionaries, on peut utiliser ce code
			//// on n'est pas obligé de calculer le hash code de tous les éléments de l'objet!
			//var items = m_items;
			//int h = 17;
			//int n = 4;
			//foreach(var kvp in items)
			//{
			//	h = (h * 31) + kvp.Key.GetHashCode();
			//	h = (h * 31) + kvp.Value.GetHashCode();
			//	if (n-- == 0) break;
			//}
			//h ^= items.Count;
			//return h;
		}

		public override int CompareTo(JsonValue? other)
		{
			throw new NotSupportedException("JSON Object cannot be compared with other elements");
		}

		#endregion

		public ExpandoObject ToExpando()
		{
			var expando = new ExpandoObject();
			var map = (IDictionary<string, object?>) expando;
			foreach (var kvp in m_items)
			{
				map.Add(kvp.Key, kvp.Value.ToObject());
			}
			return expando;
		}

		public KeyValuePair<string, JsonValue>[] ToArray()
		{
			var res = new KeyValuePair<string, JsonValue>[m_items.Count];
			CopyTo(res, 0);
			return res;
		}

		public void CopyTo(KeyValuePair<string, JsonValue>[] array)
		{
			((ICollection<KeyValuePair<string, JsonValue>>) m_items).CopyTo(array, 0);
		}

		public void CopyTo(KeyValuePair<string, JsonValue>[] array, int arrayIndex)
		{
			((ICollection<KeyValuePair<string, JsonValue>>) m_items).CopyTo(array, arrayIndex);
		}

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteByte('{');
			bool first = true;
			foreach (var kv in this)
			{
				// par défaut, on ne sérialise pas les "Missing"
				if (kv.Value.IsMissing()) break;

				if (first)
				{
					first = false;
				}
				else
				{
					writer.WriteByte(',');
				}

				if (JsonEncoding.NeedsEscaping(kv.Key))
				{
					writer.WriteStringUtf8(JsonEncoding.EncodeSlow(kv.Key));
				}
				else
				{
					writer.WriteByte('"');
					writer.WriteStringUtf8(kv.Key);
					writer.WriteByte('"');
				}
				writer.WriteByte(':');
				kv.Value.WriteTo(ref writer);
			}
			writer.WriteByte('}');
		}

	}

}
