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
	using System.Collections.Generic;

	public partial class JsonObject
	{

		[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
		public static class ReadOnly
		{

			#region Create...

			/// <summary>Returns a <b>read-only</b> empty object, that cannot be modified</summary>
			/// <remarks>
			/// <para>This method will always return <see cref="JsonObject.EmptyReadOnly"/> singleton.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create()"/></para>
			/// </remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Create() => EmptyReadOnly;

			/// <summary>Creates a new empty read-only JSON object</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <remarks>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string}?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Create(IEqualityComparer<string>? comparer)
			{
				comparer ??= StringComparer.Ordinal;
				return ReferenceEquals(comparer, StringComparer.Ordinal) ? EmptyReadOnly : new(new(0, comparer), readOnly: true);
			}

			/// <summary>Creates a new immutable JSON object with a single field</summary>
			/// <param name="key0">Name of the field</param>
			/// <param name="value0">Value of the field</param>
			/// <returns>JSON object of size 1, that cannot be modified.</returns>
			/// <remarks>For a mutable object, see <see cref="JsonObject.Create(string,JsonValue?)"/></remarks>
			[Pure]
			public static JsonObject Create(string key0, JsonValue? value0) => new(new(1, StringComparer.Ordinal)
			{
				[key0] = (value0 ?? JsonNull.Null).ToReadOnly()
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with a single field</summary>
			/// <param name="item">Name and value of the field</param>
			/// <returns>JSON object of size 1, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			public static JsonObject Create((string Key, JsonValue? Value) item) => new(new(1, StringComparer.Ordinal)
			{
				[item.Key] = (item.Value ?? JsonNull.Null).ToReadOnly()
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with 2 fields</summary>
			/// <param name="item1">Name and value of the first field</param>
			/// <param name="item2">Name and value of the second field</param>
			/// <returns>JSON object of size 2, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
			public static JsonObject Create((string Key, JsonValue? Value) item1, (string Key, JsonValue? Value) item2) => new(new(2, StringComparer.Ordinal)
			{
				[item1.Key] = (item1.Value ?? JsonNull.Null).ToReadOnly(),
				[item2.Key] = (item2.Value ?? JsonNull.Null).ToReadOnly(),
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with 3 fields</summary>
			/// <param name="item1">Name and value of the first field</param>
			/// <param name="item2">Name and value of the second field</param>
			/// <param name="item3">Name and value of the third field</param>
			/// <returns>JSON object of size 2, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			[Obsolete("Please use the JsonObject.ReadOnly.Create([ (k, v), ... ]) instead.")]
			public static JsonObject Create(
				(string Key, JsonValue? Value) item1,
				(string Key, JsonValue? Value) item2,
				(string Key, JsonValue? Value) item3
			) => new(new(3, StringComparer.Ordinal)
			{
				[item1.Key] = (item1.Value ?? JsonNull.Null).ToReadOnly(),
				[item2.Key] = (item2.Value ?? JsonNull.Null).ToReadOnly(),
				[item3.Key] = (item3.Value ?? JsonNull.Null).ToReadOnly(),
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with 4 fields</summary>
			/// <param name="item1">Name and value of the first field</param>
			/// <param name="item2">Name and value of the second field</param>
			/// <param name="item3">Name and value of the third field</param>
			/// <param name="item4">Name and value of the fourth field</param>
			/// <returns>JSON object of size 2, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			[Obsolete("Please use the JsonObject.ReadOnly.Create([ (k, v), ... ]) instead.")]
			public static JsonObject Create(
				(string Key, JsonValue? Value) item1,
				(string Key, JsonValue? Value) item2,
				(string Key, JsonValue? Value) item3,
				(string Key, JsonValue? Value) item4
			) => new(new(4, StringComparer.Ordinal)
			{
				[item1.Key] = (item1.Value ?? JsonNull.Null).ToReadOnly(),
				[item2.Key] = (item2.Value ?? JsonNull.Null).ToReadOnly(),
				[item3.Key] = (item3.Value ?? JsonNull.Null).ToReadOnly(),
				[item4.Key] = (item4.Value ?? JsonNull.Null).ToReadOnly(),
			}, readOnly: true);

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IDictionary{string,JsonValue})"/></para>
			/// </remarks>
			public static JsonObject Create(IDictionary<string, JsonValue> items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(null, items).AddRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ReadOnlySpan{KeyValuePair{string,JsonValue}},IEqualityComparer{string}?)"/></para>
			/// </remarks>
			public static JsonObject Create(ReadOnlySpan<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
			{
				return CreateEmptyWithComparer(comparer).AddRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ReadOnlySpan{ValueTuple{string,JsonValue}},IEqualityComparer{string}?)"/></para>
			/// </remarks>
			public static JsonObject Create(ReadOnlySpan<(string Key, JsonValue? Value)> items, IEqualityComparer<string>? comparer = null)
			{
				return CreateEmptyWithComparer(comparer).AddRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(KeyValuePair{string,JsonValue}[],IEqualityComparer{string}?)"/></para>
			/// </remarks>
			public static JsonObject Create(KeyValuePair<string, JsonValue>[] items, IEqualityComparer<string>? comparer = null)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer).AddRangeReadOnly(items.AsSpan()).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue}[],IEqualityComparer{string}?)"/></para>
			/// </remarks>
			public static JsonObject Create((string Key, JsonValue?)[] items, IEqualityComparer<string>? comparer = null)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer).AddRangeReadOnly(items.AsSpan()).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEnumerable{KeyValuePair{string,JsonValue}},IEqualityComparer{string}?)"/></para>
			/// </remarks>
			public static JsonObject Create(IEnumerable<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer, items).AddRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEnumerable{ValueTuple{string,JsonValue}},IEqualityComparer{string}?)"/></para>
			/// </remarks>
			public static JsonObject Create(IEnumerable<(string Key, JsonValue Value)> items, IEqualityComparer<string>? comparer = null)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer).AddRangeReadOnly(items).FreezeUnsafe();
			}

			#endregion

			#region FromValues...

			//TODO: BUGBUG: add CrystalJsonSettings parameter to all these methods!

			/// <summary>Creates a read-only JSON Object from a list of key/value pairs.</summary>
			/// <typeparam name="TValue"></typeparam>
			/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>Corresponding JSON object, that cannot be modified.</returns>
			public static JsonObject FromValues<TValue>(IEnumerable<KeyValuePair<string, TValue>> items, IEqualityComparer<string>? comparer = null)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer, items).AddValuesReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a read-only JSON Object from an existing dictionary, using a custom JSON converter.</summary>
			/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
			/// <param name="members">Dictionary that must be converted.</param>
			/// <param name="valueSelector">Handler that is called for each value of the dictionary, and must return the converted JSON value.</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>Corresponding JSON object, that cannot be modified.</returns>
			public static JsonObject FromValues<TValue>(IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
			{
				Contract.NotNull(members);

				comparer ??= ExtractKeyComparer(members) ?? StringComparer.Ordinal;

				var items = new Dictionary<string, JsonValue>(members.Count, comparer);
				foreach (var kvp in members)
				{
					items.Add(kvp.Key, (valueSelector(kvp.Value) ?? JsonNull.Missing).ToReadOnly());
				}
				return new(items, readOnly: true);
			}

			/// <summary>Creates a read-only JSON Object from a sequence of elements, using a custom key and value selector.</summary>
			/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
			/// <param name="source">Sequence of elements to convert</param>
			/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
			/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding JSON value.</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>Corresponding JSON object, that cannot be modified</returns>
			public static JsonObject FromValues<TElement>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
			{
				var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer ?? StringComparer.Ordinal);
				foreach (var item in source)
				{
					map.Add(keySelector(item), (valueSelector(item) ?? JsonNull.Missing).ToReadOnly());
				}
				return new(map, readOnly: true);
			}

			/// <summary>Creates a read-only JSON Object from a sequence of elements, using a custom key and value selector.</summary>
			/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
			/// <typeparam name="TValue">Type of the extracted values, that must support conversion to JSON values</typeparam>
			/// <param name="source">Sequence of elements to convert</param>
			/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
			/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding value, that will in turn be converted into JSON.</param>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <returns>Corresponding JSON object, that cannot be modified</returns>
			public static JsonObject FromValues<TElement, TValue>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
			{
				var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer ?? StringComparer.Ordinal);
				var context = new CrystalJsonDomWriter.VisitingContext();
				foreach (var item in source)
				{
					var child = FromValue(CrystalJsonDomWriter.DefaultReadOnly, ref context, valueSelector(item));
					Contract.Debug.Assert(child.IsReadOnly);
					map.Add(keySelector(item), child);
				}
				return new(map, readOnly: true);
			}

			#endregion

		}

		#region CopyAndXYZ()...

		private static void MakeReadOnly(Dictionary<string, JsonValue> items)
		{
			foreach (var kv in items)
			{
				if (!kv.Value.IsReadOnly)
				{
					items[kv.Key] = kv.Value.ToReadOnly();
				}
			}
		}

		/// <summary>Returns a new read-only copy of this object with an additional item</summary>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, an exception will be thrown.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, an exception will be thrown.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonObject CopyAndAdd(string key, JsonValue? value)
		{
			// copy and add the new value
			var items = new Dictionary<string, JsonValue>(m_items);
			items.Add(key, value?.ToReadOnly() ?? JsonNull.Null);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Replaces a published JSON Object with a new version with an added field, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Object</param>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, an exception will be thrown.</param>
		/// <param name="value">Value of the field to add</param>
		/// <returns>New published JSON Object, that includes the new field.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Object with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonObject CopyAndAdd(ref JsonObject original, string key, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndAdd(key, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndAddSpin(ref original, key, value);

			static JsonObject CopyAndAddSpin(ref JsonObject original, string key, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndAdd(key, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new read-only copy of this object with an additional item</summary>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, the method will return <see langword="false"/>.</param>
		/// <param name="value">Value of the new item</param>
		/// <param name="copy">Receives a new instance with the same content of the original object, plus the additional item</param>
		/// <returns><see langword="true"/> if the field was added, or <see langword="false"/> if there was already a field with the same name.</returns>
		/// <remarks>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public bool TryCopyAndAdd(string key, JsonValue? value, [MaybeNullWhen(false)] out JsonObject copy)
		{
			if (m_items.ContainsKey(key))
			{
				copy = null;
				return false;
			}

			// copy and add the new value
			var items = new Dictionary<string, JsonValue>(m_items);
			items.Add(key, value?.ToReadOnly() ?? JsonNull.Null);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			copy = new(items, readOnly: true);
			return true;
		}

		/// <summary>Returns a new read-only copy of this object, with an additional field</summary>
		/// <param name="key">Name of the field to set. If a field with the same name already exists, its previous value will be overwritten.</param>
		/// <param name="value">Value of the new field</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, its value will be overwritten.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonObject CopyAndSet(string key, JsonValue? value)
		{
			// copy and set the new value
			var items = new Dictionary<string, JsonValue>(m_items);
			items[key] = value?.ToReadOnly() ?? JsonNull.Null;

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Replaces a published JSON Object with a new version with an added field, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Object</param>
		/// <param name="key">Name of the field to set. If a field with the same name already exists, its previous value will be overwritten.</param>
		/// <param name="value">Value of the field.</param>
		/// <returns>New published JSON Object, that includes the new field.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Object with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonObject CopyAndSet(ref JsonObject original, string key, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndSet(key, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndSetSpin(ref original, key, value);

			static JsonObject CopyAndSetSpin(ref JsonObject original, string key, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndSet(key, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new read-only copy of this object, with an additional field</summary>
		/// <param name="key">Name of the new field</param>
		/// <param name="value">Value of the new field</param>
		/// <param name="previous">If the field was already present, receives its previous value. If not, receives <see langword="null"/>.</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, its value will be overwritten and the previous value will be stored in <paramref name="previous"/>.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonObject CopyAndSet(string key, JsonValue? value, out JsonValue? previous)
		{
			var items = new Dictionary<string, JsonValue>(m_items);

			// get the previous value if it exists
			items.TryGetValue(key, out previous);
			// set the new value
			items[key] = value?.ToReadOnly() ?? JsonNull.Null;

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object without the specifield item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(string key)
		{
			var items = m_items;
			if (!items.ContainsKey(key))
			{ // the key does not exist so there will be no changes
				return m_readOnly ? this : ToReadOnly();
			}

			if (items.Count == 1)
			{ // we already now key is contained in the object, so if it's the only one, the object will become empty.
				return EmptyReadOnly;
			}

			// copy and remove
			items = new(items);
			items.Remove(key);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object without the specifield item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <param name="previous"></param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(string key, out JsonValue? previous)
		{
			var items = m_items;
			if (!items.TryGetValue(key, out previous))
			{ // the key does not exist so there will be no changes
				return m_readOnly ? this : ToReadOnly();
			}

			if (items.Count == 1)
			{ // we already now key is contained in the object, so if it's the only one, the object will become empty.
				return EmptyReadOnly;
			}

			// copy and remove
			items = new(items);
			items.Remove(key);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Replaces a published JSON Object with a new version without the specified field, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Object</param>
		/// <param name="key">Name of the field to remove. If the field was not present, the object will not be changed.</param>
		/// <returns>New published JSON Object without the field, or the original object if the was not present.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Object with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonObject CopyAndRemove(ref JsonObject original, string key)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndRemove(key);
			if (ReferenceEquals(copy, snapshot))
			{ // the field did not exist
				return snapshot;
			}

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndRemoveSpin(ref original, key);

			static JsonObject CopyAndRemoveSpin(ref JsonObject original, string key)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndRemove(key);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		#endregion

	}

}
