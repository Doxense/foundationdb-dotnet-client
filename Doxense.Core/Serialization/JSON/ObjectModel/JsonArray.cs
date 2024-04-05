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

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using NodaTime;

	// Contract annotation mappings
	using CollectionAccess = JetBrains.Annotations.CollectionAccessAttribute;
	using CollectionAccessType = JetBrains.Annotations.CollectionAccessType;
	using ContractAnnotation = JetBrains.Annotations.ContractAnnotationAttribute;
	using ImplicitUseTargetFlags = JetBrains.Annotations.ImplicitUseTargetFlags;
	using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;
	using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;
	using Pure = System.Diagnostics.Contracts.PureAttribute;
	using UsedImplicitly = JetBrains.Annotations.UsedImplicitlyAttribute;

	/// <summary>Array of JSON values</summary>
	[Serializable]
	[DebuggerDisplay("JSON Array[{m_size}] {GetCompactRepresentation(0),nq}")]
	[DebuggerTypeProxy(typeof(DebugView))]
	[DebuggerNonUserCode]
	[JetBrains.Annotations.PublicAPI]
	public sealed class JsonArray : JsonValue, IList<JsonValue>, IReadOnlyList<JsonValue>, IEquatable<JsonArray>
	{
		/// <summary>Taille initiale de l'array</summary>
		internal const int DEFAULT_CAPACITY = 4;
		/// <summary>Capacité maximale pour conserver le buffer en cas de clear</summary>
		internal const int MAX_KEEP_CAPACITY = 1024;
		/// <summary>Capacité maximale pour la croissance automatique</summary>
		internal const int MAX_GROWTH_CAPACITY = 0X7FEFFFFF;

		private JsonValue[] m_items;
		private int m_size;
		private bool m_readOnly;

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private class DebugView
		{
			private readonly JsonArray m_array;

			public DebugView(JsonArray array) => m_array = array;

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public JsonValue[] Items => m_array.ToArray();
		}

		#region Constructors...

		/// <summary>Retourne une nouvelle array vide</summary>
		[Obsolete("OLD_API: Use JsonArray.CreateEmpty() instead, or use JsonArray.EmptyReadOnly or a readonly emtpy singleton")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonArray Empty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new([], 0, readOnly: false);
		}

		/// <summary>Return an empty, read-only, <see cref="JsonArray">JSON Array</see> singleton</summary>
		/// <remarks>This instance cannot be modified, and should be used to reduce memory allocations when working with read-only JSON</remarks>
		public static readonly JsonArray EmptyReadOnly = new([], 0, readOnly: true);

		/// <summary>Crée une nouvelle array vide</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray()
		{
			m_items = [];
		}

		/// <summary>Crée une nouvelle array avec une capacité initiale</summary>
		/// <param name="capacity">Capacité initiale</param>
		public JsonArray(int capacity)
		{
			if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(capacity), "Non-negative number required.");

			m_items = capacity == 0 ? [] : new JsonValue[capacity];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonArray(JsonValue[] items, int count, bool readOnly)
		{
			Contract.Debug.Requires(items != null && count <= items.Length);
			m_items = items;
			m_size = count;
			m_readOnly = readOnly;
#if FULL_DEBUG
			for (int i = 0; i < count; i++) Contract.Debug.Requires(items[i] != null, "Array cannot contain nulls");
#endif
		}

		/// <summary>Fill all occurrences of <see langword="null"/> with <see cref="JsonNull.Null"/> in the specified array</summary>
		private static void FillNullValues(Span<JsonValue?> items)
		{
			for (int i = 0; i < items.Length; i++)
			{
				items[i] ??= JsonNull.Null;
			}
		}

		/// <summary>Test if there is at least one mutable element in the specified array</summary>
		private static bool CheckAnyMutable(ReadOnlySpan<JsonValue> items)
		{
			foreach (var item in items)
			{
				if (!item.IsReadOnly)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Freeze this object and all its children, and mark it as read-only.</summary>
		/// <remarks>
		/// <para>This is similar to the <c>{ get; init; }</c> pattern for CLR records, and allows initializing a JSON object and then marking it as read-only once it is ready to be returned and/or shared, without performing extra memory allocations.</para>
		/// <para>Please note that, once "frozen", the operation cannot be reverted, and if additional mutations are required, a new copy of the object must be created.</para>
		/// </remarks>
		public override JsonArray Freeze()
		{
			if (!m_readOnly)
			{
				// we need to ensure that all children are also frozen!
				foreach(var item in AsSpan())
				{
					item.Freeze();
				}
				m_readOnly = true;
			}

			return this;
		}

		/// <summary>[DANGEROUS] Mark this array as read-only, without performing any extra checks!</summary>
		internal JsonArray FreezeUnsafe()
		{
			if (m_readOnly && m_size == 0)
			{ // ensure that we always return the empty singleton !
				return EmptyReadOnly;
			}
			m_readOnly = true;
			return this;
		}

		/// <summary>Return an new immutable read-only version of this <see cref="JsonArray">JSON Array</see> (and all of its children)</summary>
		/// <returns>The same object, if it is already read-only; otherwise, a deep copy marked as read-only.</returns>
		/// <remarks>A JSON object that is immutable is truly safe against any modification, including of any of its direct or indirect children.</remarks>
		public override JsonArray ToReadOnly()
		{
			if (m_readOnly)
			{ // already immutable
				return this;
			}

			var items = AsSpan();
			var res = new JsonValue[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = items[i].ToReadOnly();
			}
			return new(res, items.Length, readOnly: true);
		}

		/// <summary>Return a new mutable copy of this <see cref="JsonArray">JSON Array</see> (and all of its children)</summary>
		/// <returns>A deep copy of this array and its children.</returns>
		/// <remarks>
		/// <para>This will recursively copy all JSON objects or arrays present in the array, even if they are already mutable.</para>
		/// </remarks>
		public override JsonArray Copy()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return new JsonArray();

			var buf = new JsonValue[items.Length];
			// copy all children
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].Copy();
			}
			return new JsonArray(buf, items.Length, readOnly: false);
		}

		/// <summary>Create a copy of this array</summary>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the array, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		[Pure]
		protected internal override JsonArray Copy(bool deep, bool readOnly) => Copy(this, deep, readOnly);

		/// <summary>Create a copy of a <see cref="JsonArray">JSON Array</see></summary>
		/// <param name="array"><see cref="JsonArray">JSON Array</see> to clone</param>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of <paramref name="array"/>, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public static JsonArray Copy(JsonArray array, bool deep, bool readOnly = false)
		{
			Contract.NotNull(array);

			if (readOnly)
			{
				return array.ToReadOnly();
			}

			if (array.Count == 0)
			{ // empty mutable singleton
				return new JsonArray();
			}

			var items = array.AsSpan();
			if (!deep)
			{
				return new JsonArray(items.ToArray(), items.Length, readOnly);
			}

			// copy all children
			var buf = new JsonValue[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].Copy();
			}
			return new JsonArray(buf, items.Length, readOnly: false);
		}

		#region Create [JsonValue] ...

		// these methods take the items as JsonValue, and create a new mutable array that wraps them

		#region Mutable...

		/// <summary>Create a new empty array, that can be modified</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create() => new();

		/// <summary>Create a new JsonArray that will hold a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue? value) => new([
			value ?? JsonNull.Null
		], 1, readOnly: false);

		/// <summary>Create a new JsonArray that will hold a pair of elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue? value1, JsonValue? value2) => new([
			value1 ?? JsonNull.Null,
			value2 ?? JsonNull.Null
		], 2, readOnly: false);

		/// <summary>Create a new JsonArray that will hold three elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue? value1, JsonValue? value2, JsonValue? value3) => new([
			value1 ?? JsonNull.Null,
			value2 ?? JsonNull.Null,
			value3 ?? JsonNull.Null
		], 3, readOnly: false);

		/// <summary>Create a new JsonArray that will hold four elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue? value1, JsonValue? value2, JsonValue? value3, JsonValue? value4) => new([
			value1 ?? JsonNull.Null,
			value2 ?? JsonNull.Null,
			value3 ?? JsonNull.Null,
			value4 ?? JsonNull.Null
		], 4, readOnly: false);

		/// <summary>Create a new JsonArray using a list of elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(params JsonValue?[] values)
		{
			//REVIEW: TODO: when C# supports "params Span<T>" we should switch so that we have Create(params ReadOnlySpan<JsonValue?>) and Create(JsonValue?[])
			Contract.NotNull(values);
			return values.Length == 0 ? new JsonArray() : new JsonArray().AddRange(values.AsSpan()!);
		}

		#endregion

		#region Immutable...

		/// <summary>Create a new JsonArray that will hold a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray CreateReadOnly(JsonValue? value) => new([
			(value ?? JsonNull.Null).ToReadOnly()
		], 1, readOnly: true);

		/// <summary>Create a new JsonArray that will hold a pair of elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray CreateReadOnly(JsonValue? value1, JsonValue? value2) => new([
			(value1 ?? JsonNull.Null).ToReadOnly(),
			(value2 ?? JsonNull.Null).ToReadOnly()
		], 2, readOnly: true);

		/// <summary>Create a new JsonArray that will hold three elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray CreateReadOnly(JsonValue? value1, JsonValue? value2, JsonValue? value3) => new([
			(value1 ?? JsonNull.Null).ToReadOnly(),
			(value2 ?? JsonNull.Null).ToReadOnly(),
			(value3 ?? JsonNull.Null).ToReadOnly()
		], 3, readOnly: true);

		/// <summary>Create a new JsonArray that will hold four elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray CreateReadOnly(JsonValue? value1, JsonValue? value2, JsonValue? value3, JsonValue? value4) => new([
			(value1 ?? JsonNull.Null).ToReadOnly(),
			(value2 ?? JsonNull.Null).ToReadOnly(),
			(value3 ?? JsonNull.Null).ToReadOnly(),
			(value4 ?? JsonNull.Null).ToReadOnly()
		], 4, readOnly: true);

		[Pure]
		public static JsonArray CreateReadOnly(params JsonValue?[] items)
		{
			//REVIEW: TODO: when C# supports "params Span<T>" we should switch so that we have Create(params ReadOnlySpan<JsonValue?>) and Create(JsonValue?[])
			Contract.NotNull(items);
			return items.Length == 0 ? EmptyReadOnly : new JsonArray().AddRangeReadOnly(items.AsSpan()!).FreezeUnsafe();
		}

		#endregion

		#endregion

		#region Copy [JsonValues] ...

		#region Mutable...

		/// <summary>Create a new JsonArray using a list of elements</summary>
		/// <param name="values">Elements of the new array</param>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray Copy(ReadOnlySpan<JsonValue> values)
		{
			return new JsonArray().AddRange(values!);
		}

		/// <summary>Create a new JsonArray using a list of elements</summary>
		/// <param name="values">Elements of the new array</param>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray Copy(JsonValue[] values)
		{
			Contract.NotNull(values);
			return new JsonArray().AddRange(values.AsSpan()!);
		}

		/// <summary>Create a new JsonArray using a list of elements</summary>
		/// <param name="values">Elements of the new array</param>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray Copy(IEnumerable<JsonValue?> values)
		{
			return new JsonArray().AddRange(values);
		}

		#endregion

		#region Immutable...

		/// <summary>Create a new JsonArray from a list of elements</summary>
		[Pure]
		public static JsonArray CopyReadOnly(ReadOnlySpan<JsonValue> items)
		{
			return items.Length == 0 ? EmptyReadOnly : new JsonArray().AddRangeReadOnly(items!).FreezeUnsafe();
		}

		/// <summary>Create a new JsonArray from a list of elements</summary>
		public static JsonArray CopyReadOnly(JsonValue[] items)
		{
			Contract.NotNull(items);
			return items.Length == 0 ? EmptyReadOnly : new JsonArray().AddRangeReadOnly(items.AsSpan()!).FreezeUnsafe();
		}

		/// <summary>Create a new JsonArray from a list of elements</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public static JsonArray CopyReadOnly(IEnumerable<JsonValue?> items)
		{
			Contract.NotNull(items);

			return items.TryGetNonEnumeratedCount(out var count) && count == 0
				? EmptyReadOnly
				: new JsonArray().AddRangeReadOnly(items).FreezeUnsafe();
		}

		#endregion

		#endregion

		#region FromValues [of T] ...

		#region Mutable...

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableu d'éléments dont le type est connu.</summary>
		/// <typeparam name="TValue">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les éléments du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TValue>(ReadOnlySpan<TValue> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return new JsonArray().AddValues<TValue>(values, settings, resolver);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableu d'éléments dont le type est connu.</summary>
		/// <typeparam name="TValue">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les éléments du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TValue>(TValue[] values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(values);
			return new JsonArray().AddValues<TValue>(values.AsSpan(), settings, resolver);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une séquence d'éléments dont le type est connu.</summary>
		/// <typeparam name="TValue">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Séquences d'éléments à convertir</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les éléments de la séquence, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TValue>(IEnumerable<TValue> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return new JsonArray().AddValues<TValue>(values, settings, resolver);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableau d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments du tableau</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les valeurs du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TItem, TValue>(ReadOnlySpan<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(selector);
			return new JsonArray().AddValues(values, selector, settings, resolver);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableau d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments du tableau</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les valeurs du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TItem, TValue>(TItem[] values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(values);
			return new JsonArray().AddValues(values.AsSpan(), selector, settings, resolver);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une séquence d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments de la séquence</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="values">Séquences d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les valeurs de la séquence, convertis en JsonValue</returns>
		[Pure]
		public static JsonArray FromValues<TItem, TValue>(IEnumerable<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return new JsonArray().AddValues(values, selector, settings, resolver);
		}

		#endregion

		#region Immutable...

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableu d'éléments dont le type est connu.</summary>
		/// <typeparam name="TValue">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les éléments du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValuesReadOnly<TValue>(ReadOnlySpan<TValue> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return values.Length == 0 ? EmptyReadOnly : new JsonArray().AddValuesReadOnly<TValue>(values, settings, resolver).FreezeUnsafe();
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableu d'éléments dont le type est connu.</summary>
		/// <typeparam name="TValue">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les éléments du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValuesReadOnly<TValue>(TValue[] values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(values);
			return values.Length == 0 ? EmptyReadOnly : new JsonArray().AddValuesReadOnly<TValue>(values.AsSpan(), settings, resolver).FreezeUnsafe();
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une séquence d'éléments dont le type est connu.</summary>
		/// <typeparam name="TValue">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Séquences d'éléments à convertir</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les éléments de la séquence, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValuesReadOnly<TValue>(IEnumerable<TValue> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(values);
			return values.TryGetNonEnumeratedCount(out var count) && count == 0
				? EmptyReadOnly
				: new JsonArray().AddValuesReadOnly<TValue>(values, settings, resolver).FreezeUnsafe();
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableau d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments du tableau</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les valeurs du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValuesReadOnly<TItem, TValue>(ReadOnlySpan<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(selector);
			return values.Length == 0 ? EmptyReadOnly : new JsonArray().AddValuesReadOnly(values, selector, settings, resolver).FreezeUnsafe();
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableau d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments du tableau</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les valeurs du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValuesReadOnly<TItem, TValue>(TItem[] values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(values);
			return values.Length == 0 ? EmptyReadOnly : new JsonArray().AddValuesReadOnly(values.AsSpan(), selector, settings, resolver).FreezeUnsafe();
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une séquence d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments de la séquence</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="values">Séquences d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>JsonArray contenant tous les valeurs de la séquence, convertis en JsonValue</returns>
		[Pure]
		public static JsonArray FromValuesReadOnly<TItem, TValue>(IEnumerable<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(values);
			return values.TryGetNonEnumeratedCount(out var count) && count == 0
				? EmptyReadOnly
				: new JsonArray().AddValuesReadOnly(values, selector, settings, resolver).FreezeUnsafe();
		}

		#endregion

		#endregion

		#region Tuples...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1>(ValueTuple<T1> tuple) => new([FromValue<T1>(tuple.Item1)], 1, readOnly: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2>(ValueTuple<T1, T2> tuple) => new([FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2)], 2, readOnly: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3>(ValueTuple<T1, T2, T3> tuple) => new([FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3)], 3, readOnly: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> tuple) => new([FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4)], 4, readOnly: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> tuple) => new([FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5)], 5, readOnly: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5, T6>(ValueTuple<T1, T2, T3, T4, T5, T6> tuple) => new([FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5), FromValue<T6>(tuple.Item6)], 6, readOnly: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5, T6, T7>(ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple) => new([FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5), FromValue<T6>(tuple.Item6), FromValue<T7>(tuple.Item7)], 7, readOnly: false);

		#endregion

		/// <summary>Combine two JsonArrays into a single new array</summary>
		/// <remarks>The new array is contain a copy of the items of the two input arrays</remarks>
		public static JsonArray Combine(JsonArray arr1, JsonArray arr2)
		{
			// combine the items
			int size1 = arr1.m_size;
			int size2 = arr2.m_size;
			var newSize = checked(size1 + size2);

			var tmp = new JsonValue[newSize];
			arr1.AsSpan().CopyTo(tmp);
			arr2.AsSpan().CopyTo(tmp.AsSpan(size1));

			return new JsonArray(tmp, newSize, readOnly: false);
		}

		/// <summary>Combine three JsonArrays into a single new array</summary>
		/// <remarks>The new array is contain a copy of the items of the three input arrays</remarks>
		public static JsonArray Combine(JsonArray arr1, JsonArray arr2, JsonArray arr3)
		{
			// combine the items
			int size1 = arr1.m_size;
			int size2 = arr2.m_size;
			int size3 = arr3.m_size;
			var newSize = checked(size1 + size2 + size3);

			var tmp = new JsonValue[newSize];
			arr1.CopyTo(tmp);
			arr2.CopyTo(tmp.AsSpan(size1));
			arr3.CopyTo(tmp.AsSpan(size1 + size2));

			return new JsonArray(tmp, newSize, readOnly: false);
		}

		#endregion

		public override JsonType Type => JsonType.Array;

		/// <summary>Une Array ne peut pas être null</summary>
		public override bool IsNull => false;

		/// <summary>La valeur par défaut pour une array est null, donc retourne toujours false</summary>
		public override bool IsDefault => false;

		public override bool IsReadOnly => m_readOnly;

		/// <summary>Indique si le tableau est vide</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("OLD_API: Use 'arr.Count == 0' instead.")]
		public bool IsEmpty //REVIEW: remove this? (Count == 0)
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_size == 0;
		}

		#region List<T>...

		[EditorBrowsable(EditorBrowsableState.Always)]
		public int Count
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_size;
		}

		/// <summary>Fixe ou retourne la capacité interne de l'array</summary>
		/// <remarks>
		/// A utiliser avant d'insérer un grand nombre d'éléments si la taille finale est connue.
		/// Si la nouvelle capacité est inférieure au nombre d'items existants, une exception est générée.
		/// </remarks>
		public int Capacity
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_items.Length;
			set
			{
				if (value < m_size) throw ThrowHelper.ArgumentOutOfRangeException(nameof(value), value, "capacity was less than the current size.");
				if (value != m_items.Length)
				{
					ResizeBuffer(value);
				}
			}
		}

		/// <summary>Vérifie la capacité du buffer interne, et resize le ci besoin</summary>
		/// <param name="min">Taille minimum du buffer</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureCapacity(int min)
		{
			if (m_items.Length < min)
			{
				GrowBuffer(min);
			}
		}

		/// <summary>Resize le buffer pour accomoder un certain nombre d'items</summary>
		/// <param name="min">Nombre minimum d'éléments nécessaires</param>
		/// <remarks>Double la taille du buffer interne jusqu'a ce qu'il puisse stocker au moins <paramref name="min"/> items</remarks>
		private void GrowBuffer(int min)
		{
			// on veut garder la taille d'origine en multiple de 2 si possible

			long newCapacity = Math.Max(m_items.Length, DEFAULT_CAPACITY);
			while (newCapacity < min) newCapacity <<= 1;

			// au dela de MAX_GROWTH_CAPACITY, on stop le factor de x2 (et donc forte dégradation des perfs)
			if (newCapacity > MAX_GROWTH_CAPACITY)
			{
				newCapacity = MAX_GROWTH_CAPACITY;
				if (newCapacity < min) throw ThrowHelper.InvalidOperationException("Cannot resize JSON array because it would exceed the maximum allowed size");
			}
			ResizeBuffer((int) newCapacity);
		}

		/// <summary>Redimenssione le buffer interne</summary>
		/// <param name="size">Taille exacte du nouveau buffer</param>
		/// <remarks>Les éléments existants sont copiés, les nouveaux slots sont remplis de null</remarks>
		private void ResizeBuffer(int size)
		{
			if (size > 0)
			{
				var tmp = new JsonValue[size];
				this.AsSpan().CopyTo(tmp);
				m_items = tmp;
			}
			else
			{
				m_items = [];
			}
		}

		/// <summary>Returns a span of all items in this array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Span<JsonValue> AsSpan() => m_items.AsSpan(0, m_size);

		/// <summary>Réajuste la taille du buffer interne, afin d'optimiser la consommation mémoire</summary>
		/// <remarks>A utiliser après un batch d'insertion, si on sait qu'on n'ajoutera plus d'items dans la liste</remarks>
		public void TrimExcess()
		{
			// uniquement si on gagne au moins 10%
			int threshold = (int) (m_items.Length * 0.9);
			if (m_size < threshold)
			{
				this.Capacity = m_size;
			}
		}

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal void ThrowCannotMutateReadOnlyObject() => throw FailCannotMutateReadOnlyValue(this);

		/// <inheritdoc cref="JsonValue.this[int]"/>
		[AllowNull] // only for the setter
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue this[int index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(index);
			set
			{
				if (m_readOnly) ThrowCannotMutateReadOnlyObject();
				Contract.Debug.Requires(!ReferenceEquals(this, value));
				m_items.AsSpan(0, m_size)[index] = value ?? JsonNull.Null;
			}
		}

		/// <inheritdoc cref="JsonValue.this[Index]"/>
		[AllowNull] // only for the setter
		public override JsonValue this[Index index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(index);
			set
			{
				if (m_readOnly) ThrowCannotMutateReadOnlyObject();
				Contract.Debug.Requires(!ReferenceEquals(this, value));
				m_items.AsSpan(0, m_size)[index] = value ?? JsonNull.Null;
			}
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Add(JsonValue? value)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.Debug.Requires(!ReferenceEquals(this, value));
			// invariant: adding 'null' will add JsonNull.Null
			int size = m_size;
			if (size == m_items.Length)
			{
				EnsureCapacity(size + 1);
			}
			m_items[size] = value ?? JsonNull.Null;
			m_size = size + 1;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddNull()
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Add(JsonNull.Null);
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddValue<TValue>(TValue value)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Add(FromValue<TValue>(value));
		}

		#region AddRange...

		//note: AddRange(..) est appelé par beaucoup de helpers comme ToJsonArray(), CreateRange(), ....
		// => c'est ici qu'on doit centraliser toute la logique d'optimisations (pour éviter d'en retrouver partout)

		#region AddRange [JsonValue] ...

		/// <summary>Append une autre array en copiant ou clonant ses éléments à la fin</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(ReadOnlySpan<JsonValue?> array)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			if (array.Length > 0)
			{
				int size = m_size;
				EnsureCapacity(size + array.Length);

				var items = m_items;
				Contract.Debug.Assert(items != null && size + array.Length <= items.Length);

				var tail = items.AsSpan(size, array.Length);
				array.CopyTo(tail!);
				FillNullValues(tail!);
				m_size = size + array.Length;
			}
			return this;
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(JsonValue[] items)
		{
			Contract.NotNull(items);
			return AddRange(items.AsSpan()!);
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRangeReadOnly(JsonValue[] items)
		{
			Contract.NotNull(items);
			return AddRangeReadOnly(items.AsSpan()!);
		}

		/// <summary>Append une autre array en copiant ou clonant ses éléments à la fin</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRangeReadOnly(ReadOnlySpan<JsonValue?> array)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			if (array.Length > 0)
			{
				// resize
				int size = m_size;
				EnsureCapacity(size + array.Length);
				var items = m_items;
				Contract.Debug.Assert(items != null && size + array.Length <= items.Length);

				// append
				var tail = items.AsSpan(size, array.Length);
				for (int i = 0; i < tail.Length; i++)
				{
					tail[i] = (array[i] ?? JsonNull.Null).ToReadOnly();
				}
				m_size = size + array.Length;
			}
			return this;
		}

		/// <summary>Append une séquence d'éléments en copiant ou clonant ses éléments à la fin</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(IEnumerable<JsonValue?> items)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);

			// JsonArray
			if (items is JsonArray jarr)
			{ // optimized
				return AddRange(jarr.GetSpan()!);
			}

			// Regular Array
			if (items is JsonValue?[] arr)
			{ // optimized
				return AddRange(arr.AsSpan());
			}

			// Collection
			if (items is List<JsonValue?> list)
			{
				return AddRange(CollectionsMarshal.AsSpan(list));
			}

			if (items.TryGetNonEnumeratedCount(out var count))
			{ // we can pre-allocate to the new size and copy into tail of the buffer

				int newSize = checked(m_size + count);
				EnsureCapacity(newSize);

				// append to the tail
				var tail = m_items.AsSpan(m_size, count);
				int i = 0;
				foreach (var item in items)
				{
					tail[i] = item ?? JsonNull.Null;
				}
				m_size = newSize;
			}
			else
			{ // we don't know the size in advance, we may need to resize multiple times
				foreach (var item in items)
				{
					Add(item ?? JsonNull.Null);
				}
			}

			return this;
		}

		/// <summary>Append une séquence d'éléments en copiant ou clonant ses éléments à la fin</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRangeReadOnly(IEnumerable<JsonValue?> items)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);

			// JsonArray
			if (items is JsonArray jarr)
			{ // optimized
				return AddRangeReadOnly(jarr.GetSpan()!);
			}

			// Regular Array
			if (items is JsonValue?[] arr)
			{ // optimized
				return AddRangeReadOnly(arr.AsSpan());
			}

			// Collection
			if (items is List<JsonValue?> list)
			{
				return AddRangeReadOnly(CollectionsMarshal.AsSpan(list));
			}

			if (items.TryGetNonEnumeratedCount(out var count))
			{
				// pre-resize to the new capacity
				int newSize = checked(m_size + count);
				EnsureCapacity(newSize);

				// append to the tail
				var tail = m_items.AsSpan(m_size, count);
				int i = 0;
				foreach (var item in items)
				{
					tail[i] = (item ?? JsonNull.Null).ToReadOnly();
				}
				m_size = newSize;
			}
			else
			{
				// may trigger multiple resizes!
				foreach (var item in items)
				{
					Add((item ?? JsonNull.Null).ToReadOnly());
				}
			}

			return this;
		}

		#endregion

		#region AddRange [of T] ...

		#region Mutable...

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TValue>(ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			if (items.Length == 0) return this;

			// pré-alloue si on connait à l'avance la taille
			int newSize = checked(this.Count + items.Length);
			EnsureCapacity(newSize);
			var tail = m_items.AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T>
				for(int i = 0; i < items.Length; i++)
				{
					tail[i] = FromValue<TValue>(items[i]); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			if (typeof(TValue) == typeof(JsonValue))
			{
				var json = MemoryMarshal.CreateReadOnlySpan<JsonValue?>(ref Unsafe.As<TValue, JsonValue?>(ref MemoryMarshal.GetReference(items)), items.Length);
				return AddRange(json);
			}

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = dom.ParseObjectInternal(ref context, items[i], type, null);
			}
			m_size = newSize;
			return this;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TValue>(TValue[] items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValues<TValue>(items.AsSpan(), settings, resolver);
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TValue>(IEnumerable<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);

			if (items is TValue[] arr)
			{ // fast path for arrays
				return AddValues<TValue>(arr.AsSpan(), settings, resolver);
			}

			if (items is List<TValue> list)
			{ // fast path for lists
				return AddValues<TValue>(CollectionsMarshal.AsSpan(list), settings, resolver);
			}

			if (items is IEnumerable<JsonValue?> json)
			{ // fast path if already JSON values
				return AddRange(json);
			}

			if (items.TryGetNonEnumeratedCount(out var count))
			{ // pre-allocated to the new size 
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we should have a JIT optimized version of As<T> for these as well!
				foreach (var item in items)
				{
					Add(FromValue<TValue>(item));
				}
				return this;
			}

#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, value, type, null));
			}
			return this;
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TSource">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		/// <param name="settings"></param>
		/// <param name="resolver"></param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TSource, TValue>(ReadOnlySpan<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(transform);
			if (items.Length == 0) return this;

			// il y a plus de chances que items soit une liste/array/collection (sauf s'il y a un Where(..) avant)
			// donc ca vaut le coup tenter de pré-allouer l'array
			int newSize = checked(this.Count + items.Length);
			EnsureCapacity(newSize);
			var tail = m_items.AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T>
				for(int i = 0; i < tail.Length; i++)
				{
					tail[i] = FromValue(transform(items[i])); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			for(int i = 0; i < tail.Length; i++)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				tail[i] = dom.ParseObjectInternal(ref context, transform(items[i]), type, null);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TSource">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		/// <param name="settings"></param>
		/// <param name="resolver"></param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TSource, TValue>(TSource[] items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValues(items.AsSpan(), transform, settings, resolver);
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TSource">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		/// <param name="settings"></param>
		/// <param name="resolver"></param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TSource, TValue>(IEnumerable<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);
			Contract.NotNull(transform);

			if (items is TSource[] arr)
			{
				return AddValues(arr.AsSpan(), transform, settings, resolver);
			}

			if (items is List<TSource> list)
			{
				return AddValues(CollectionsMarshal.AsSpan(list), transform, settings, resolver);
			}

			// il y a plus de chances que items soit une liste/array/collection (sauf s'il y a un Where(..) avant)
			// donc ca vaut le coup tenter de pré-allouer l'array
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			   )
			{
				// we shoud have a JIT optimized version of FromValue<T> for these as well
				foreach (var item in items)
				{
					Add(FromValue(transform(item)));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var item in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, transform(item), type, null));
			}
			return this;
		}

		#endregion

		#region Immutable...

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TValue>(ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			if (items.Length == 0) return this;

			// pré-alloue si on connait à l'avance la taille
			int newSize = checked(this.Count + items.Length);
			EnsureCapacity(newSize);
			var tail = m_items.AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T> for these as well
				for(int i = 0; i < items.Length; i++)
				{
					tail[i] = FromValue<TValue>(items[i]); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			if (typeof(TValue) == typeof(JsonValue))
			{
				// force cast to a ReadOnlySpan<JsonValue>
				var json = MemoryMarshal.CreateReadOnlySpan<JsonValue?>(ref Unsafe.As<TValue, JsonValue?>(ref MemoryMarshal.GetReference(items)), items.Length);
				// then add these values directly
				return AddRangeReadOnly(json);
			}

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			for (int i = 0; i < items.Length; i++)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				tail[i] = dom.ParseObjectInternal(ref context, items[i], type, null);
			}
			m_size = newSize;
			return this;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TValue>(TValue[] items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValuesReadOnly<TValue>(items.AsSpan(), settings, resolver);
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TValue>(IEnumerable<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);

			if (items is TValue[] arr)
			{ // fast path for arrays
				return AddValuesReadOnly<TValue>(arr.AsSpan(), settings, resolver);
			}

			if (items is List<TValue> list)
			{ // fast path for lists
				return AddValuesReadOnly<TValue>(CollectionsMarshal.AsSpan(list), settings, resolver);
			}

			if (items is IEnumerable<JsonValue?> json)
			{ // fast path if already JSON values
				return AddRangeReadOnly(json);
			}

			if (items.TryGetNonEnumeratedCount(out var count))
			{ // pre-allocated to the new size 
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we should have a JIT optimized version of As<T> for these as well!
				foreach (var item in items)
				{
					Add(FromValue<TValue>(item));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.CreateReadOnly(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, value, type, null));
			}
			return this;
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TSource">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		/// <param name="settings"></param>
		/// <param name="resolver"></param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TSource, TValue>(ReadOnlySpan<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(transform);
			if (items.Length == 0) return this;

			// il y a plus de chances que items soit une liste/array/collection (sauf s'il y a un Where(..) avant)
			// donc ca vaut le coup tenter de pré-allouer l'array
			int newSize = checked(this.Count + items.Length);
			EnsureCapacity(newSize);
			var tail = m_items.AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T>
				for(int i = 0; i < tail.Length; i++)
				{
					tail[i] = FromValue(transform(items[i])); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.CreateReadOnly(settings ?? CrystalJsonSettings.JsonReadOnly, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			for(int i = 0; i < tail.Length; i++)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				tail[i] = dom.ParseObjectInternal(ref context, transform(items[i]), type, null);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TSource">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		/// <param name="settings"></param>
		/// <param name="resolver"></param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TSource, TValue>(TSource[] items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValuesReadOnly(items.AsSpan(), transform, settings, resolver);
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TSource">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		/// <param name="settings"></param>
		/// <param name="resolver"></param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TSource, TValue>(IEnumerable<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);
			Contract.NotNull(transform);

			if (items is TSource[] arr)
			{
				return AddValuesReadOnly(arr.AsSpan(), transform, settings, resolver);
			}

			if (items is List<TSource> list)
			{
				return AddValuesReadOnly(CollectionsMarshal.AsSpan(list), transform, settings, resolver);
			}

			// il y a plus de chances que items soit une liste/array/collection (sauf s'il y a un Where(..) avant)
			// donc ca vaut le coup tenter de pré-allouer l'array
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we shoud have a JIT optimized version of FromValue<T> for these as well
				foreach (var item in items)
				{
					Add(FromValue<TValue>(transform(item)));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.CreateReadOnly(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var item in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, transform(item), type, null));
			}
			return this;
		}

		#endregion

		#endregion

		#region AddRange [object] (boxed) ...

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		internal JsonArray AddRangeBoxed(IEnumerable<object?> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			//note: internal pour éviter que ce soit trop facile de passer par 'object' au lieu de 'T'
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);

			// si c'est déjà une collection de JsonValue, on n'a pas besoin de les convertir
			if (items is IEnumerable<JsonValue?> values)
			{
				AddRange(values);
				return this;
			}

			settings ??= CrystalJsonSettings.Json;
			resolver ??= CrystalJson.DefaultResolver;

			// pré-alloue si on connaît à l'avance la taille
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(JsonValue.FromValue(value, settings, resolver));
			}

			return this;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		internal JsonArray AddRangeBoxedReadOnly(IEnumerable<object?> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			//note: internal pour éviter que ce soit trop facile de passer par 'object' au lieu de 'T'
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(items);

			// si c'est déjà une collection de JsonValue, on n'a pas besoin de les convertir
			if (items is IEnumerable<JsonValue?> values)
			{
				AddRangeReadOnly(values);
				return this;
			}

			settings = (settings ?? CrystalJsonSettings.Json).AsReadOnly();
			resolver ??= CrystalJson.DefaultResolver;

			// pré-alloue si on connaît à l'avance la taille
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(JsonValue.FromValue(value, settings, resolver));
			}

			return this;
		}

		#endregion

		#endregion


		#region CopyAndXYZ...

		private static void MakeReadOnly(Span<JsonValue> items)
		{
			for(int i = 0; i < items.Length; i++)
			{
				if (!items[i].IsReadOnly)
				{
					items[i] = items[i].ToReadOnly();
				}
			}
		}

		/// <summary>Returns a new read-only copy of this array with an additional item</summary>
		/// <param name="value">Item that will be appended to the end of the new copy</param>
		/// <returns>A new instance with the same content of the original array, plus the additional item</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndAdd(JsonValue? value)
		{
			value = value?.ToReadOnly() ?? JsonNull.Null;

			if (m_size == 0)
			{
				return new JsonArray([value], 1, readOnly: true);
			}

			// copy and add the new value
			int newSize = checked(m_size + 1);
			var items = new JsonValue[newSize];
			this.AsSpan().CopyTo(items);
			items[m_size] = value;

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an added item, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="value">Value of the field to append</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndAdd(ref JsonArray original, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndAdd(value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndAddSpin(ref original, value);

			static JsonArray CopyAndAddSpin(ref JsonArray original, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndAdd(value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new read-only copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, except the additional item at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndSet(int index, JsonValue? value) => CopyAndSet(index, value, out _);

		/// <summary>Returns a new read-only copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, except the additional item at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		public JsonArray CopyAndSet(Index index, JsonValue? value) => CopyAndSet(index.GetOffset(m_size), value, out _);

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an new item at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndSet(ref JsonArray original, int index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndSet(index, value, out _);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndSetSpin(ref original, index, value);

			static JsonArray CopyAndSetSpin(ref JsonArray original, int index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndSet(index, value, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an new item at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndSet(ref JsonArray original, Index index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndSet(index, value, out _);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndSetSpin(ref original, index, value);

			static JsonArray CopyAndSetSpin(ref JsonArray original, Index index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndSet(index, value, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new read-only copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index where to write the new item</param>
		/// <param name="value">Value of the new item</param>
		/// <param name="previous">Receives the previous value at this location, or <see langword="null"/> if the index is outside the bounds of the array.</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndSet(int index, JsonValue? value, out JsonValue? previous)
		{
			if (index < 0) throw new IndexOutOfRangeException("Index of outside the bounds of the JSON Array");

			// copy and set the new value
			JsonValue[] items;
			int newSize;
			if (index < m_size)
			{ // update in place
				items = this.AsSpan().ToArray();
				newSize = m_size;
				previous = items[index];
			}
			else
			{ // update outside the array, must resize
				newSize = checked(index + 1);
				items = new JsonValue[newSize];
				var prev = this.AsSpan();
				prev.CopyTo(items);
				if (index > prev.Length)
				{ // fill the gap!
					items.AsSpan(prev.Length, index - prev.Length).Fill(JsonNull.Null);
				}
				previous = null;
			}
			items[index] = value?.ToReadOnly() ?? JsonNull.Null;

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index where to write the new item</param>
		/// <param name="value">Value of the new item</param>
		/// <param name="previous">Receives the previous value at this location, or <see langword="null"/> if the index is outside the bounds of the array.</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndSet(Index index, JsonValue? value, out JsonValue? previous) => CopyAndSet(index.GetOffset(m_size), value, out previous);

		/// <summary>Returns a new read-only copy of this array, with a new item inserted at the specified location</summary>
		/// <param name="index">Index where to insert to insert, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>A new instance with the same content of the original object, with the additional item inserted at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndInsert(int index, JsonValue? value)
		{
			if (index < 0) throw new IndexOutOfRangeException("Index of outside the bounds of the JSON Array");

			value = value?.ToReadOnly() ?? JsonNull.Null;

			// copy and set the new value
			JsonValue[] items;
			int newSize;
			if (m_size == 0)
			{ // add to empty array
				newSize = checked(index + 1);
				items = new JsonValue[newSize];
				// fill with nulls
				if (index > 0)
				{
					items.AsSpan(0, index).Fill(JsonNull.Null);
				}
				// insert item
				items[index] = value;
			}
			else if (index < m_size)
			{ // insert inside the array, must shift the tail
				newSize = checked(m_size + 1);
				var prev = this.AsSpan();
				items = new JsonValue[newSize];
				// copy head
				prev[..index].CopyTo(items);
				// insert item
				items[index] = value;
				// copy tail
				prev[index..].CopyTo(items.AsSpan(index + 1));
			}
			else
			{ // insert outside the array, must fill gaps with nulls
				newSize = checked(index + 1);
				items = new JsonValue[newSize];
				var prev = this.AsSpan();
				prev.CopyTo(items);
				if (index > prev.Length)
				{ // please, mind the gap!
					items.AsSpan(prev.Length, index - prev.Length).Fill(JsonNull.Null);
				}
				items[index] = value;
			}

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this array, with a new item inserted at the specified location</summary>
		/// <param name="index">Index where to insert the item, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>A new instance with the same content of the original object, with the additional item inserted at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		public JsonArray CopyAndInsert(Index index, JsonValue? value) => CopyAndSet(index.GetOffset(m_size), value);

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an item inserted at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index where to insert the item, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndInsert(ref JsonArray original, int index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndInsert(index, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndInsertSpin(ref original, index, value);

			static JsonArray CopyAndInsertSpin(ref JsonArray original, int index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndInsert(index, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an item inserted at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index where to insert the item, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndInsert(ref JsonArray original, Index index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndInsert(index, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndInsertSpin(ref original, index, value);

			static JsonArray CopyAndInsertSpin(ref JsonArray original, Index index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndInsert(index, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		//TODO: CopyAndRemove ?

		/// <summary>Returns a new read-only copy of this array without the specifield item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(int index) => CopyAndRemove(index, out _);

		/// <summary>Returns a new read-only copy of this array without the specifield item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(Index index) => CopyAndRemove(index.GetOffset(m_size), out _);

		/// <summary>Returns a new read-only copy of this array without the specifield item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <param name="previous">Receives the value that was removed, or <see langword="null"/> if the index was outside the bounds of the array</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(int index, out JsonValue? previous)
		{
			if (index < 0) throw new IndexOutOfRangeException("Index of outside the bounds of the JSON Array");

			var prev = this.AsSpan();
			if (index >= prev.Length)
			{ // the index is outside the bounds, no changes
				previous = null;
				return m_readOnly ? this : ToReadOnly();
			}

			if (prev.Length == 1)
			{ // removing the last item
				Contract.Debug.Assert(index == 0);
				previous = prev[0];
				return EmptyReadOnly;
			}

			// copy and remove
			var items = new JsonValue[prev.Length - 1];
			if (index > 0)
			{ // copy head
				prev[..index].CopyTo(items);
			}
			previous = prev[index];
			if (index < prev.Length - 1)
			{ // copy tail
				prev[(index + 1)..].CopyTo(items.AsSpan(index));
			}

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, items.Length, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this array without the specifield item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <param name="previous">Receives the value that was removed, or <see langword="null"/> if the index was outside the bounds of the array</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(Index index, out JsonValue? previous) => CopyAndRemove(index.GetOffset(m_size), out previous);

		/// <summary>Replaces a published JSON Array with a new version without the specified item, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Array</param>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>New published JSON Array without the field, or the original arrray if the was not present.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Array with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndRemove(ref JsonArray original, int index)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndRemove(index, out _);
			if (ReferenceEquals(copy, snapshot))
			{ // the field did not exist
				return snapshot;
			}

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndRemoveSpin(ref original, index);

			static JsonArray CopyAndRemoveSpin(ref JsonArray original, int index)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndRemove(index, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Replaces a published JSON Array with a new version without the specified item, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Array</param>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>New published JSON Array without the field, or the original arrray if the was not present.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Array with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndRemove(ref JsonArray original, Index index)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndRemove(index, out _);
			if (ReferenceEquals(copy, snapshot))
			{ // the field did not exist
				return snapshot;
			}

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndRemoveSpin(ref original, index);

			static JsonArray CopyAndRemoveSpin(ref JsonArray original, Index index)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndRemove(index, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		//TODO: CopyAndSwap ?

		#endregion

		/// <summary>Supprime tous les éléments de cette array</summary>
		/// <remarks>Si le buffer est inférieur à 1024, il est conservé.
		/// Pour supprimer le buffer, il faut appeler <see cref="TrimExcess()"/> juste après <see cref="Clear()"/></remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void Clear()
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();

			int size = m_size;
			if (size > 0 && size <= MAX_KEEP_CAPACITY)
			{ // pas trop grand, on garde le buffer
				Array.Clear(m_items, 0, size);
			}
			else
			{ // clear
				m_items = [];
			}
			m_size = 0;
			//TODO: versionning?
		}

		[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValue(int index) => m_items.AsSpan(0, m_size)[index].RequiredIndex(index);

		[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValue(Index index) => m_items.AsSpan(0, m_size)[index].RequiredIndex(index);
		
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public override bool TryGetValue(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if ((uint) index < m_size)
			{
				value = m_items[index];
				return true;
			}
			value = default;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public override bool TryGetValue(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			var offset = index.GetOffset(m_size);
			if ((uint) offset < m_size)
			{
				value = m_items[offset];
				return true;
			}
			value = default;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(int index, JsonValue? defaultValue = null)
		{
			if ((uint) index >= m_size)
			{
				return defaultValue ?? JsonNull.Error;
			}
			var child = m_items[index];
			return child is not (null or JsonNull) ? child : defaultValue ?? JsonNull.Null;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(Index index, JsonValue? defaultValue = null)
		{
			var offset = index.GetOffset(m_size);
			if ((uint) offset >= m_size)
			{
				return defaultValue ?? JsonNull.Error;
			}
			var child = m_items[offset];
			return child is not (null or JsonNull) ? child : defaultValue ?? JsonNull.Null;
		}

		/// <summary>Determines the index of a specific value in the <see cref="JsonArray">JSON Array</see>.</summary>
		/// <param name="item">The value to locate in the array.</param>
		/// <returns>The index of <paramref name="item" /> if found in the array; otherwise, <see langword="-1"/>.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public int IndexOf(JsonValue item) => this.AsSpan().IndexOf(item);

		/// <summary>Determines whether the <see cref="JsonArray">JSON Array</see> contains a specific JSON value.</summary>
		/// <param name="item">The value to locate in the array.</param>
		/// <returns> <see langword="true" /> if <paramref name="item" /> is found in the array; otherwise, <see langword="false" />.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override bool Contains(JsonValue? item)
		{
			var items = m_items;
			var size = m_size;
			if (item == null)
			{
				for (int i = 0; i < size; i++)
				{
					if (items[i].IsNullOrMissing()) return true;
				}
				return false;
			}
			else
			{
				for (int i = 0; i < size; i++)
				{
					if (item.Equals(items[i])) return true;
				}
				return false;
			}
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Set(int index, JsonValue? item)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			if (index < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));

			if (index < m_size)
			{
				m_items[index] = item ?? JsonNull.Null;
			}
			else
			{
				SetAfterResize(index, item);
			}
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Set(Index index, JsonValue? item)
		{
			Set(index.GetOffset(m_size), item);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void SetAfterResize(int index, JsonValue? item)
		{
			var size = m_size;
			Contract.Debug.Requires(index >= size);
			EnsureCapacity(index + 1);
			var items = m_items;
			int gap = index - size;
			if (gap > 0) m_items.AsSpan(size, gap).Fill(JsonNull.Null);
			items[index] = item ?? JsonNull.Null;
			m_size = index + 1;
		}

		/// <summary>Inserts an item to the <see cref="JsonArray">JSON Array</see> at the specified index.</summary>
		/// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The object to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Insert(int index, JsonValue? item)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			var size = m_size;
			if ((uint) index > size) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));

			if (m_size == m_items.Length)
			{
				EnsureCapacity(size + 1);
			}

			var items = m_items;
			if (index < size)
			{
				items.AsSpan(index, size - index).CopyTo(items.AsSpan(index + 1));
			}

			items[index] = item ?? JsonNull.Null;
			m_size = size + 1;
			//TODO: versionning?
		}

		/// <summary>Inserts an item to the <see cref="JsonArray">JSON Array</see> at the specified index.</summary>
		/// <param name="index">The index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The object to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Insert(Index index, JsonValue? item)
		{
			Insert(index.GetOffset(m_size), item);
		}

		/// <summary>Removes the first occurrence of a specific value from the <see cref="JsonArray">JSON Array</see>.</summary>
		/// <param name="item">The value to remove from the array.</param>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		/// <returns><see langword="true" /> if <paramref name="item" /> was successfully removed from the array; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if <paramref name="item" /> is not found in the original array.</returns>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public bool Remove(JsonValue item)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();

			int index = IndexOf(item);
			if (index >= 0)
			{
				RemoveAt(index);
				return true;
			}
			return false;
		}

		/// <summary>Removes the item at the specified index.</summary>
		/// <param name="index">The zero-based index of the item to remove.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveAt(int index)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			var size = m_size;
			if ((uint) index >= size) ThrowHelper.ThrowArgumentOutOfRangeException();

			--size;
			var items = m_items;
			if (index < size)
			{
				items.AsSpan(index + 1, size - index).CopyTo(items.AsSpan(index));
			}
		
			items[size] = default!; // clear the reference to prevent any GC leak!
			m_size = size;
		}

		/// <summary>Removes the item at the specified index.</summary>
		/// <param name="index">The index of the item to remove.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveAt(Index index)
		{
			RemoveAt(index.GetOffset(m_size));
		}

		/// <summary>Swap two items in this <see cref="JsonArray">JSON Array</see></summary>
		/// <param name="first">Index of the first item</param>
		/// <param name="second">Index of the second item</param>
		/// <exception cref="ArgumentOutOfRangeException">If any of <paramref name="first"/> or <paramref name="second"/> is outside the bounds of the array</exception>
		/// <example><c>[ 1, 2, 3, 4 ].Swap(1, 2) === [ 1, 3, 2, 4 ]</c></example>
		public void Swap(int first, int second)
		{
			if ((uint) first >= m_size) throw new ArgumentOutOfRangeException(nameof(first), first, "The first index is outside the bounds of the array");
			if ((uint) second >= m_size) throw new ArgumentOutOfRangeException(nameof(second), second, "The second index is outside the bounds of the array");

			if (first != second)
			{ // swap both items
				(m_items[second], m_items[first]) = (m_items[first], m_items[second]);
			}
		}

		/// <summary>Swap two items in this <see cref="JsonArray">JSON Array</see></summary>
		/// <param name="first">Index of the first item</param>
		/// <param name="second">Index of the second item</param>
		/// <exception cref="ArgumentOutOfRangeException">If any of <paramref name="first"/> or <paramref name="second"/> is outside the bounds of the array</exception>
		/// <example><c>[ 1, 2, 3, 4 ].Swap(^2, ^1) === [ 1, 2, 4, 3 ]</c></example>
		public void Swap(Index first, Index second) => Swap(first.GetOffset(m_size), second.GetOffset(m_size));

		/// <summary>Copies the contents of this <see cref="JsonArray">JSON Array</see> into a destination <see cref="T:System.Span`1" />.</summary>
		/// <param name="destination">The destination <see cref="T:System.Span`1" /> object.</param>
		/// <exception cref="T:System.ArgumentException"><paramref name="destination" /> is shorter than the source <see cref="T:System.Span`1" />.</exception>
		[CollectionAccess(CollectionAccessType.Read)]
		public void CopyTo(Span<JsonValue> destination) => this.AsSpan().CopyTo(destination);

		/// <summary>Attempts to copy the contents of this <see cref="JsonArray">JSON Array</see> to a destination <see cref="T:System.Span`1" /> and returns a value that indicates whether the copy operation succeeded.</summary>
		/// <param name="destination">The target of the copy operation.</param>
		/// <returns> <see langword="true" /> if the copy operation succeeded; otherwise, <see langword="false" />.</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool TryCopyTo(Span<JsonValue> destination) => this.AsSpan().TryCopyTo(destination);

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void CopyTo(JsonValue[] array, int arrayIndex) => this.AsSpan().CopyTo(array.AsSpan(arrayIndex));
		//TODO: REVIEW: make this explicit? (to force callers to use the Span overload)

		#endregion

		#region Operators...

		/// <summary>Keep only the elements that match a predicate</summary>
		/// <param name="predicate">Predicate that should returns <see langword="true"/> for elements to keep, and <see langword="false"/> for elements to discard</param>
		/// <returns>Number of elements that where kept</returns>
		/// <remarks>The original array is modified</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public int KeepOnly([InstantHandle] Func<JsonValue, bool> predicate)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(predicate);

			// if already empty, nothing much to do
			if (m_size == 0) return 0;

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (predicate(items[i]))
				{
					items[p++] = i;
				}
			}
			Contract.Debug.Ensures(p <= m_size);
			if (p < m_size)
			{
				items.AsSpan(p, m_size - p).Clear();
			}
			m_size = p;
			return p;
		}

		/// <summary>Remove all the elements that match a predicate</summary>
		/// <param name="predicate">Predicate that returns <see langword="true"/> for elements to remove, and <see langword="false"/> for elements to keep</param>
		/// <returns>Number of elements that where removed</returns>
		/// <remarks>The original array is modified</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public int RemoveAll([InstantHandle] Func<JsonValue, bool> predicate)
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			Contract.NotNull(predicate);

			// if already empty, nothing much to do
			if (m_size == 0) return 0;

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (!predicate(items[i]))
				{
					items[p++] = i;
				}
			}
			int r = m_size - p;
			Contract.Debug.Assert(r >= 0);
			if (r > 0)
			{
				items.AsSpan(p, r).Clear();
			}
			m_size = p;
			return r;
		}

		/// <summary>Remove duplicate elements from this array</summary>
		/// <remarks>Similar to Distinct(), except that it modifies the original array.</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveDuplicates()
		{
			if (m_readOnly) ThrowCannotMutateReadOnlyObject();
			if (m_size <= 1)
			{ // no duplicates possible
				return;
			}

			// Two pass algorithm:
			// 1) copy all the elements in a set
			//    - if count == m_size, no duplicates => EXIT
			// 2) for each element:
			//    - if in set, keep it, remove from set
			//    - if not int set, remove it

			var set = new HashSet<JsonValue>(this, EqualityComparer<JsonValue>.Default); //TODO: JsonValueComparer!
			if (set.Count == m_size)
			{ // no duplicates found
				return;
			}

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (set.Remove(items[i]))
				{ // it was in the set, keep it
					items[p++] = items[i];
				}
				//else: it has already been copied
			}
			Contract.Debug.Assert(p == set.Count);
			if (p < m_size)
			{
				items.AsSpan(p, m_size - p).Clear();
			}
			m_size = p;
		}

		/// <summary>Returns a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items starting from the specified index</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(int index)
		{
			if (index == 0 && m_readOnly)
			{ // return the whole immutable array
				return this;
			}

			// get the corresponding slice
			var tmp = this.AsSpan().Slice(index);
			if (tmp.Length == 0)
			{ // empty
				return m_readOnly ? EmptyReadOnly : new JsonArray();
			}

			// return a new array wrapping these items
			return new JsonArray(tmp.ToArray(), tmp.Length, m_readOnly);
		}

		/// <summary>Returns a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items starting from the specified index</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(Index index)
		{
			return GetRange(index.GetOffset(m_size));
		}

		/// <summary>Returns a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items in the specified range</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(int index, int count)
		{
			if (index == 0 && count == m_size && m_readOnly)
			{ // return the whole immutable array
				return this;
			}

			// get the corresponding slice
			var tmp = this.AsSpan().Slice(index, count);
			if (tmp.Length == 0)
			{ // empty
				return m_readOnly ? EmptyReadOnly : new JsonArray();
			}

			// return a new array wrapping these items
			return new JsonArray(tmp.ToArray(), tmp.Length, m_readOnly);
		}

		/// <summary>Return a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items in the specified range</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(Range range)
		{
			var (index, count) = range.GetOffsetAndLength(m_size);
			return GetRange(index, count);
		}

		/// <summary>Returns a read-only span of all items in this array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan() => this.AsSpan();

		/// <summary>Returns a read-only span of the items in this array, starting from the specified index</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan(int start) => this.AsSpan().Slice(start);

		/// <summary>Returns a read-only span of the items in this array, starting from the specified index for a specified length</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan(int start, int length) => this.AsSpan().Slice(start, length);

		/// <summary>Returns a read-only span of the items in this array, for the specified range</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan(Range range) => this.AsSpan()[range];

		#endregion

		#region Enumerator...

		public Enumerator GetEnumerator() => new(m_items, m_size);

		IEnumerator<JsonValue> IEnumerable<JsonValue>.GetEnumerator() => new Enumerator(m_items, m_size);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(m_items, m_size);

		public struct Enumerator : IEnumerator<JsonValue>
		{
			private readonly JsonValue[] m_items;
			private readonly int m_size;
			private int m_index;
			private JsonValue? m_current;

			internal Enumerator(JsonValue[] items, int size)
			{
				m_items = items;
				m_size = size;
				m_index = 0;
				m_current = null;
			}

			public void Dispose()
			{ }

			public bool MoveNext()
			{
				if ((uint) m_index < m_size)
				{
					m_current = m_items[m_index];
					m_index++;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				m_index = m_size + 1;
				m_current = null;
				return false;
			}

			object IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index == m_size + 1)
					{
						ThrowHelper.ThrowInvalidOperationException("Operation cannot happen.");
					}
					return this.Current;
				}
			}

			void IEnumerator.Reset()
			{
				m_index = 0;
				m_current = null;
			}

			public JsonValue Current => m_current!;
		}

		/// <summary>Retourne une vue typée de cette <see cref="JsonArray"/> comme si elle ne contenait que des <see cref="JsonObject"/>s</summary>
		/// <param name="required">Si <see langword="true"/>, vérifie que chaque élément de l'array n'est pas null</param>
		/// <remarks>
		/// Toute entrée contenant un <see cref="JsonNull"/> retournera <b>null</b>!
		/// Si l'array contient autre chose que des <see cref="JsonObject"/>, une <see cref="InvalidCastException"/> sera générée au runtime lors de l'énumération!
		/// </remarks>
		public JsonArray<JsonObject> AsObjects(bool required = false)
		{
			return new JsonArray<JsonObject>(m_items, m_size, required);
		}

		/// <summary>Retourne une vue typée de cette <see cref="JsonArray"/> comme si elle ne contenait que des <see cref="JsonArray"/>s</summary>
		/// <param name="required">Si <see langword="true"/>, vérifie que chaque élément de l'array n'est pas null</param>
		/// <remarks>
		/// Toute entrée contenant un <see cref="JsonNull"/> retournera <b>null</b>!
		/// Si l'array contient autre chose que des <see cref="JsonArray"/>, une <see cref="InvalidCastException"/> sera générée au runtime lors de l'énumération!
		/// </remarks>
		[Pure]
		public JsonArray<JsonArray> AsArrays(bool required = false)
		{
			return new JsonArray<JsonArray>(m_items, m_size, required);
		}

		/// <summary>Retourne une wrapper sur cette <see cref="JsonArray"/> qui convertit les éléments en <typeparamref name="TValue"/></summary>
		/// <remarks>Cette version est optimisée pour réduire le nombre d'allocations mémoires</remarks>
		public TypedEnumerable<TValue> Cast<TValue>(bool required = false)
		{
			//note: on ne peut pas appeler cette méthode "As<TValue>" a cause d'un conflit avec l'extension method As<TValue> sur les JsonValue!
			//=> arr.As<int[]> retourne un int[], alors que arr.Cast<int[]> serait l'équivalent d'un IEnumerable<int[]> (~= int[][]) !

			return new TypedEnumerable<TValue>(this, required);
		}

		/// <summary>Wrapper for a <see cref="JsonArray"/> that converts each element into a <typeparamref name="TValue"/>.</summary>
		public struct TypedEnumerable<TValue> : IEnumerable<TValue> //REVIEW:TODO: IList<TValue> ?
		{
			//note: this is to convert JsonValue into int, bool, string, ...

			private readonly JsonArray m_array;
			private readonly bool m_required;

			internal TypedEnumerable(JsonArray array, bool required)
			{
				m_array = array;
				m_required = required;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public TypedEnumerator GetEnumerator()
			{
				return new TypedEnumerator(m_array, m_required);
			}

			IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
			{
				return GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public TValue?[] ToArray()
			{
				//TODO:BUGBUG:m_required == true !?
				return m_array.ToArray<TValue>();
			}

			public List<TValue?> ToList()
			{
				//TODO:BUGBUG:m_required == true !?
				return m_array.ToList<TValue?>();
			}

			/// <summary>Enumerator that converts each element of a <see cref="JsonArray"/> into a TValue.</summary>
			public struct TypedEnumerator : IEnumerator<TValue>
			{
				private readonly JsonArray m_array;
				private int m_index;
				private TValue? m_current;
				private readonly bool m_required;

				internal TypedEnumerator(JsonArray array, bool required)
				{
					m_array = array;
					m_index = 0;
					m_current = default;
					m_required = required;
				}

				public void Dispose()
				{ }

				public bool MoveNext()
				{
					var arr = m_array;
					if ((uint) m_index < arr.m_size)
					{

						var val = arr.m_items[m_index];
						if (m_required && val.IsNullOrMissing())
						{
							throw FailElementMissing();
						}
						m_current = val.As<TValue>();
						m_index++;
						return true;
					}
					return MoveNextRare();
				}

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private InvalidOperationException FailElementMissing()
				{
					return new InvalidOperationException($"The JSON element at index {m_index} is null or missing");
				}

				private bool MoveNextRare()
				{
					m_index = m_array.m_size + 1;
					m_current = default;
					return false;
				}

				object IEnumerator.Current
				{
					get
					{
						if (m_index == 0 || m_index == m_array.m_size + 1)
						{
							throw ThrowHelper.InvalidOperationException("Operation cannot happen.");
						}
						return this.Current!;
					}
				}

				void IEnumerator.Reset()
				{
					m_index = 0;
					m_current = default;
				}

				public TValue Current => m_current!;

			}

		}

		#endregion

		internal override bool IsSmallValue()
		{
			const int LARGE_ARRAY = 5;

			var items = this.AsSpan();
			if (items.Length >= LARGE_ARRAY) return false;

			foreach(var item in items)
			{
				if (!item.IsSmallValue())
				{
					return false;
				}
			}

			return true;
		}

		internal override bool IsInlinable() => false;

		internal override string GetCompactRepresentation(int depth)
		{
			int size = m_size;
			if (size == 0) return "[ ]"; // empty

			var items = m_items;

			if (depth >= 3 || (depth == 2 && !IsSmallValue()))
			{
				if (size == 1)
				{
					return "[ " + items[0].GetCompactRepresentation(depth + 1) + " ]";
				}
				switch (GetElementsTypeOrDefault())
				{
					case JsonType.Number:  return size == 2 ? "[ " + items[0] + ", " + items[1] + " ]" : $"[ /* {size:N0} Numbers */ ]";
					case JsonType.Array:   return $"[ /* {size:N0} Arrays */ ]";
					case JsonType.Object:  return $"[ /* {size:N0} Objects */ ]";
					case JsonType.Boolean: return size == 2 ? "[ " + items[0] + ", " + items[1] + " ]" : $"[ /* {size:N0} Booleans */ ]";
					case JsonType.String:  return size == 2 ? "[ " + items[0] + ", " + items[1] + " ]" : $"[ /* {size:N0} Strings */ ]";
					default:               return $"[ /* {size:N0} x \u2026 */ ]";
				}
			}

			var sb = new StringBuilder(128);
			// If the size of the array is higher than 5, dump the first 4 items, followed by ", ... X more".
			// Note: If size is 5 , we will dump the fifth item, in order to not end up with ", ... 1 more" that would not be helpful and will frequently be longer than dumping the item anyway...
			++depth;
			sb.Append("[ ").Append(items[0].GetCompactRepresentation(depth));
			if (size >= 2) sb.Append(", ").Append(items[1].GetCompactRepresentation(depth));
			if (size >= 3) sb.Append(", ").Append(items[2].GetCompactRepresentation(depth));
			if (depth == 1)
			{ // we allow up to 4 items
				if (size >= 4) sb.Append(", ").Append(items[3].GetCompactRepresentation(depth));
				if (size == 5) sb.Append(", ").Append(items[4].GetCompactRepresentation(depth));
				else if (size > 5) sb.Append($", /* … {size - 4:N0} more */");
			}
			else
			{ // we allow up to 3 items
				if (size == 4) sb.Append(", ").Append(items[3].GetCompactRepresentation(depth));
				else if (size > 4) sb.Append($", /* … {size - 3:N0} more */");
			}
			sb.Append(" ]");
			return sb.ToString();
		}

		/// <summary>Converts this <see cref="JsonArray">JSON Array</see> with a <see cref="List{T}">List&lt;object?></see>.</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override object ToObject()
		{
			//TODO: détecter le cas ou tt les members ont le même type T,
			// et dans ce cas créer une List<T>, plutôt qu'une List<object> !
			//TODO2: pourquoi ne pas retourner un object[] plutôt qu'une List<object> ?

			var list = new List<object?>(this.Count);
			foreach (var value in this.AsSpan())
			{
				list.Add(value.ToObject());
			}
			return list;
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			//note: we cannot use JIT optimization here, because the type will usually be an array or list of value types, which itself is not a value type.
			return (resolver ?? CrystalJson.DefaultResolver).BindJsonArray(type, this);
		}

		/// <summary>Retourne une <see cref="JsonValue"/>[] contenant les mêmes éléments comme cette array JSON</summary>
		/// <remarks>Effectue une shallow copy des éléments.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonValue[] ToArray() => this.AsSpan().ToArray();

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public TValue?[] ToArray<TValue>(ICrystalJsonTypeResolver? resolver = null)
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we should have a JIT optimized version for these as well
				return ToPrimitiveArray<TValue>();
			}
#endif
			#endregion </JIT_HACK>

			//testé au runtime, mais il est probable que la majorité des array soient des string[] ??
			if (typeof(TValue) == typeof(string))
			{
				return (TValue[]) (object) ToStringArray();
			}

			var items = this.AsSpan();
			if (items.Length == 0)
			{
				return [];
			}

			var result = new TValue?[items.Length];
			if (resolver == null || resolver == CrystalJson.DefaultResolver)
			{
				for (int i = 0; i < result.Length; i++)
				{
					result[i] = items[i].As<TValue>();
				}
			}
			else
			{
				for (int i = 0; i < result.Length; i++)
				{
					result[i] = items[i].As<TValue>(resolver);
				}
			}
			return result;
		}

#if !DEBUG // <JIT_HACK>

		[Pure, CollectionAccess(CollectionAccessType.Read), UsedImplicitly]
		private T?[] ToPrimitiveArray<T>()
		{
			//IMPORTANT! typeof(T) doit être un type primitif reconnu par As<T> via compile time scanning!!!

			var items = this.AsSpan();
			if (items.Length == 0) return [];
			var buf = new T?[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].As<T>();
			}
			return buf;
		}

#endif

		/// <summary>Désérialise une <see cref="JsonArray">JSON Array</see> en array d'objets dont le type est défini</summary>
		/// <typeparam name="TValue">Type des éléments de la liste</typeparam>
		/// <param name="value">Tableau JSON contenant des objets a priori de type T</param>
		/// <param name="required"></param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Retourne une IList&lt;T&gt; contenant les éléments désérialisés</returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static TValue?[]? BindArray<TValue>(JsonValue? value, ICrystalJsonTypeResolver? resolver = null, bool required = false)
		{
			if (value is not JsonArray array)
			{
				return value == null || value.IsNull
					? (required ? JsonValueExtensions.FailRequiredValueIsNullOrMissing<TValue[]>() : null)
					: throw CrystalJson.Errors.Binding_CannotDeserializeJsonTypeIntoArrayOf(value, typeof(TValue));
			}

			return array.ToArray<TValue>(resolver);
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool[] ToBoolArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new bool[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToBoolean();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public int[] ToInt32Array()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new int[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToInt32();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public uint[] ToUInt32Array()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new uint[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToUInt32();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public long[] ToInt64Array()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new long[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToInt64();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ulong[] ToUInt64Array()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new ulong[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToUInt64();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public float[] ToSingleArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new float[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToSingle();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public double[] ToDoubleArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new double[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToDouble();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Half[] ToHalfArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new Half[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToHalf();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public decimal[] ToDecimalArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new decimal[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToDecimal();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Guid[] ToGuidArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new Guid[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToGuid();
			}
			return buf;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Instant[] ToInstantArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var result = new Instant[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				result[i] = items[i].ToInstant();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public string?[] ToStringArray()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var result = new string?[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				result[i] = items[i].ToStringOrDefault();
			}
			return result;
		}

		/// <summary>Return a <see cref="List{JsonValue}">List&lt;JsonValue&gt;</see> with the same elements as this array</summary>
		/// <returns>A shallow copy of the original items</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<JsonValue> ToList()
		{
			var items = this.AsSpan();
			var res = new List<JsonValue>(items.Length);
#if NET8_0_OR_GREATER
			res.AddRange(items);
#else
			foreach(var item in items)
			{
				res.Add(item);
			}
#endif
			return res;
		}

		/// <summary>Return a <see cref="List{JsonValue}">List&lt;JsonValue&gt;</see> with the same elements as this array</summary>
		/// <returns>A shallow copy of the original items</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<TValue?> ToList<TValue>(ICrystalJsonTypeResolver? resolver = null)
		{
			#region <JIT_HACK>

			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				return ToPrimitiveList<TValue>();
			}
#endif
			#endregion </JIT_HACK>

			var items = this.AsSpan();
			var list = new List<TValue?>(items.Length);
			if (resolver == null || resolver == CrystalJson.DefaultResolver)
			{
				foreach(var item in items)
				{
					list.Add(item.As<TValue>());
				}
			}
			else
			{
				foreach(var item in items)
				{
					list.Add(item.As<TValue>(resolver));
				}
			}
			return list;
		}

		/// <summary>Return a <see cref="List{T}"/> with the transformed elements of this array</summary>
		/// <param name="transform">Transforamtion that is applied on each element of the array</param>
		/// <returns>A list of all elements that have been converted <paramref name="transform"/></returns>
		[Pure]
		public List<TValue> ToList<TValue>([InstantHandle] Func<JsonValue, TValue> transform)
		{
			Contract.NotNull(transform);
			var items = this.AsSpan();
			var list = new List<TValue>(items.Length);
#if NET8_0_OR_GREATER
			// update the list in-place
			CollectionsMarshal.SetCount(list, m_size);
			var tmp = CollectionsMarshal.AsSpan(list);
			for (int i = 0; i < items.Length; i++)
			{
				tmp[i] = transform(items[i]);
			}
#else
			foreach(var item in items)
			{
				list.Add(transform(item));
			}
#endif
			return list;
		}

		/// <summary>Convert this <see cref="JsonArray">JSON Array</see> so that it, or any of its children that were previously read-only, can be mutated.</summary>
		/// <returns>The same instance if it is already fully mutable, OR a copy where any read-only Object or Array has been converted to allow mutations.</returns>
		/// <remarks>
		/// <para>Will return the same instance if it is already mutable, or a new deep copy with all children marked as mutable.</para>
		/// <para>This attempts to only copy what is necessary, and will not copy objects or arrays that are already mutable, or all other "value types" (strings, booleans, numbers, ...) that are always immutable.</para>
		/// </remarks>
		public override JsonArray ToMutable()
		{
			if (m_readOnly)
			{ // create a mutable copy
				return Copy();
			}

			// the top-level is mutable, but maybe it has read-only children?
			var items = this.GetSpan();
			JsonValue[]? copy = null;
			for(int i = 0; i < items.Length; i++)
			{
				var child = items[i];
				if (child is (JsonObject or JsonArray) && child.IsReadOnly)
				{
					copy ??= items.ToArray();
					copy[i] = child.Copy();
				}
			}

			if (copy == null)
			{ // already mutable
				return this;
			}

			return new(copy, items.Length, readOnly: false);
		}

		/// <summary>Deserialize a <see cref="JsonArray">JSON Array</see> into a list of objects of the specified type</summary>
		/// <typeparam name="TValue">Type des éléments de la liste</typeparam>
		/// <param name="value"><see cref="JsonArray">JSON Array</see> that contains the elements to bind</param>
		/// <param name="resolver">Optional type resolver</param>
		/// <param name="required">If <see langword="true"/> the array cannot be null</param>
		/// <returns>A list of all elements that have been deserialized into instance of type <typeparamref name="TValue"/></returns>
		[Pure, ContractAnnotation("value:null => null")]
		public static List<TValue?>? BindList<TValue>(JsonValue? value, ICrystalJsonTypeResolver? resolver = null, bool required = false)
		{
			if (value == null || value.IsNull) return required ? JsonValueExtensions.FailRequiredValueIsNullOrMissing<List<TValue?>>() : null;
			if (value is not JsonArray array) throw CrystalJson.Errors.Binding_CannotDeserializeJsonTypeIntoArrayOf(value, typeof(TValue));
			return array.ToList<TValue>(resolver);
		}

#if !DEBUG // <JIT_HACK>

		[Pure, CollectionAccess(CollectionAccessType.Read), UsedImplicitly]
		private List<T?> ToPrimitiveList<T>()
		{
			//IMPORTANT! T must a primitive type that is recognized by As<T> and inline by the JIT!!!
			var items = this.AsSpan();
			var result = new List<T?>(items.Length);
#if NET8_0_OR_GREATER
			if (items.Length > 0)
			{
				// update the list in-place
				CollectionsMarshal.SetCount(result, m_size);
				var tmp = CollectionsMarshal.AsSpan(result);
				Contract.Debug.Assert(tmp.Length == m_size);
				ref var ptr = ref tmp[0];
				foreach (var item in items)
				{
					ptr = item.As<T>();
					ptr = ref Unsafe.Add(ref ptr, 1);
				}
			}
#else
			foreach(var item in items)
			{
				result.Add(item.As<T>());
			}
#endif

			return result;
		}

#endif

#if NET8_0_OR_GREATER

		/// <summary>Return the sum of all the items in <see cref="JsonArray">JSON Array</see> interpreted as the corresponding <typeparamref name="TNumber"/></summary>
		/// <remarks>This is equivalent to calling <c>array.ToArray&lt;TNumber&gt;().Sum()</c></remarks>
		public TNumber Sum<TNumber>() where TNumber : System.Numerics.INumberBase<TNumber>
		{
			var total = TNumber.Zero;
			foreach (var item in this.AsSpan())
			{
				total += item.As<TNumber>(TNumber.Zero);
			}
			return total;
		}

		/// <summary>Return the average of all the items in <see cref="JsonArray">JSON Array</see> interpreted as the corresponding <typeparamref name="TNumber"/></summary>
		/// <remarks>This is equivalent to calling <c>array.ToArray&lt;TNumber&gt;().Average()</c></remarks>
		public TNumber Average<TNumber>() where TNumber : System.Numerics.INumberBase<TNumber>
		{
			return Sum<TNumber>() / TNumber.CreateChecked(this.Count);
		}

#endif

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="bool"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<bool> ToBoolList()
		{
			var result = new List<bool>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToBoolean());
			}
			return result;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="int"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<int> ToInt32List()
		{
			var result = new List<int>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToInt32());
			}
			return result;
		}

		/// <summary>Return the sum of all the items in <see cref="JsonArray">JSON Array</see> interpreted as 32-bit signed integers</summary>
		/// <remarks>This is equivalent to calling <c>array.ToArray&lt;int&gt;().Sum()</c></remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public int SumInt32()
		{
			int total = 0;
			foreach (var item in this.AsSpan())
			{
				total += item.ToInt32();
			}
			return total;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="uint"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<uint> ToUInt32List()
		{
			var result = new List<uint>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToUInt32());
			}
			return result;
		}

		/// <summary>Return the sum of all the items in <see cref="JsonArray">JSON Array</see> interpreted as 32-bit unsigned integers</summary>
		/// <remarks>This is equivalent to calling <c>array.ToArray&lt;uint&gt;().Sum()</c></remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public uint SumUInt32()
		{
			uint total = 0;
			foreach (var item in this.AsSpan())
			{
				total += item.ToUInt32();
			}
			return total;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="long"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<long> ToInt64List()
		{
			var result = new List<long>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToInt64());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public long SumInt64()
		{
			long total = 0;
			foreach (var item in this.AsSpan())
			{
				total += item.ToInt64();
			}
			return total;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="ulong"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<ulong> ToUInt64List()
		{
			var result = new List<ulong>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToUInt64());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ulong SumUInt64()
		{
			ulong total = 0;
			foreach (var item in this.AsSpan())
			{
				total += item.ToUInt64();
			}
			return total;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="float"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<float> ToSingleList()
		{
			var result = new List<float>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToSingle());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public float SumSingle() => (float) SumDouble(); // use higher precision

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="double"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<double> ToDoubleList()
		{
			var result = new List<double>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToDouble());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public double SumDouble()
		{
			double total = 0;
			foreach (var item in this.AsSpan())
			{
				total += item.ToDouble();
			}
			return total;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="double"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<double> ToHalfList()
		{
			var result = new List<double>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToDouble());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Half SumHalf() => (Half) SumDouble(); // use the best precision

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="decimal"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<decimal> ToDecimalList()
		{
			var result = new List<decimal>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToDecimal());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public decimal SumDecimal()
		{
			decimal total = 0;
			foreach (var item in this.AsSpan())
			{
				total += item.ToDecimal();
			}
			return total;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="Guid"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<Guid> ToGuidList()
		{
			var result = new List<Guid>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToGuid());
			}
			return result;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="Instant"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<Instant> ToInstantList()
		{
			var result = new List<Instant>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToInstant());
			}
			return result;
		}

		/// <summary>Deserialize this <see cref="JsonArray">JSON Array</see> into a list of <see cref="string"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<string?> ToStringList()
		{
			var result = new List<string?>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToStringOrDefault());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ImmutableList<TValue?> ToImmutableList<TValue>(ICrystalJsonTypeResolver? resolver = null)
		{
			resolver ??= CrystalJson.DefaultResolver;
			var list = ImmutableList.CreateBuilder<TValue?>();
			foreach (var item in this.AsSpan())
			{
				list.Add(item.Bind<TValue>(resolver));
			}
			return list.ToImmutable();
		}

		/// <summary>Test if there is at least one element in the array</summary>
		/// <returns><see langword="false"/> if the array is empty; otherwise, <see langword="true"/>.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool Any() => m_size > 0;

		/// <summary>Test if there is at least one element in the array with the specified type</summary>
		/// <param name="type">Type of element (<see cref="JsonType.Object"/>, <see cref="JsonType.String"/>, <see cref="JsonType.Number"/>, ...)</param>
		/// <returns><see langword="true"/> if there is at least one element of this type in the array, or <see langword="false"/> if the array is empty or contains only elements that are of a different type.</returns>
		public bool Any(JsonType type)
		{
			foreach (var item in this.AsSpan())
			{
				if (item.Type != type)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Test if there is at least one element in the array that matches the specified <paramref name="predicate"/></summary>
		/// <param name="predicate">Callback that should return <see langword="true"/> for matching elements.</param>
		/// <returns><see langword="true"/> if there is at least one element that matches, or <see langword="true"/> if the array is empty or without any matching element.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool Any([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			foreach (var item in this.AsSpan())
			{
				if (predicate(item))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Test if all the elements in the array have the specified type</summary>
		/// <param name="type">Type of element (<see cref="JsonType.Object"/>, <see cref="JsonType.String"/>, <see cref="JsonType.Number"/>, ...)</param>
		/// <remarks><see langword="false"/> if at least one element is of a different type, or <see langword="true"/> if the array is empty or with only elements of this type.</remarks>
		/// <remarks>Retourne toujours true pour une array vide.</remarks>
		public bool All(JsonType type)
		{
			foreach (var item in this.AsSpan())
			{
				if (item.Type != type)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Test if all elements in the array match the specified <paramref name="predicate"/>.</summary>
		/// <param name="predicate">Callback that should return <see langword="true"/> for matching elements.</param>
		/// <returns><see langword="false"/> if there is at least one element that does not match, or <see langword="true"/> if the array is empty or without only matching elements.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool All([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			foreach (var item in this.AsSpan())
			{
				if (!predicate(item))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Test if all elements in the array have a similar type, or if they are dissimilar</summary>
		/// <returns>The <see cref="JsonType"/> that have elements all have in common, or <see langword="null"/> if there is at least two incompatible types present.</returns>
		/// <remarks>
		/// <para>Ignore any null or missing elements in the array, sor for exemple <c>[ 123, null, 789 ]</c> will return <see cref="JsonType.Number"/>.</para>
		/// <para>Will return <see langword="null"/> if the array is only filled with null or missing values, instead of <see cref="JsonType.Null"/>.</para>
		/// </remarks>
		public JsonType? GetElementsTypeOrDefault()
		{
			JsonType? type = null;
			foreach (var item in this.AsSpan())
			{
				var t = item.Type;
				if (t == JsonType.Null) continue;

				if (type == null)
				{ // first non-null
					type = t;
				}
				else if (t != type)
				{ // different type
					return null;
				}
			}
			return type;
		}

		/// <summary>Merge the content of an array into the current array</summary>
		/// <param name="other">Source array that should be copied into the current array.</param>
		/// <param name="deepCopy">If <see langword="true"/>, clone all the elements of <paramref name="other"/> before adding them. If <see langword="false"/>, the same instance will be present in both arrays.</param>
		public void MergeWith(JsonArray other, bool deepCopy = false)
		{
			Merge(this, other, deepCopy);
		}

		/// <summary>Merge the content of an array into the current array</summary>
		/// <param name="parent">Parent array that will be modified</param>
		/// <param name="other">Source array that should be copied into the <paramref name="parent"/>.</param>
		/// <param name="deepCopy">If <see langword="true"/>, clone all the elements of <paramref name="other"/> before adding them. If <see langword="false"/>, the same instance will be present in both arrays.</param>
		public static JsonArray Merge(JsonArray parent, JsonArray other, bool deepCopy = false)
		{
			// we know how to handle:
			// - one or the other is empty
			// - both have same size

			int n = parent.Count;
			if (n == 0) return JsonArray.Copy(other, deepCopy, readOnly: false);
			if (other.Count == 0) return JsonArray.Copy(parent, deepCopy, readOnly: false);

			if (n == other.Count)
			{
				if (deepCopy) parent = parent.Copy();
				for (int i = 0; i < n; i++)
				{
					var left = parent[i];
					var right = other[i];
					switch (left.Type)
					{
						case JsonType.Object:
						{
							parent[i] = JsonObject.Merge((JsonObject) left, right.AsObject(), deepCopy);
							break;
						}
						case JsonType.Array:
						{
							parent[i] = Merge((JsonArray) left, right.AsArray(), deepCopy);
							break;
						}
						default:
						{
							parent[i] = deepCopy ? right.Copy() : right[i];
							break;
						}
					}
				}
				return parent;
			}

			//TODO: union?
			throw new NotSupportedException("Merging of JSON arrays of different sizes is not yet supported");
		}

		[CollectionAccess(CollectionAccessType.Read)]
		internal static JsonArray Project(IEnumerable<JsonValue?> source, KeyValuePair<string, JsonValue?>[] defaults)
		{
			Contract.Debug.Requires(source != null && defaults != null);

			var array = source is ICollection<JsonValue> coll ? new JsonArray(coll.Count) : new JsonArray();

			foreach (var item in source)
			{
				if (item == null)
				{
					array.Add(JsonNull.Null);
					continue;
				}

				switch (item.Type)
				{
					case JsonType.Object:
					{
						array.Add(JsonObject.Project((JsonObject) item, defaults));
						break;
					}
					case JsonType.Null:
					{ // null/JsonNull sont laissés tel quel
						array.Add(JsonNull.Null);
						break;
					}
					default:
					{
						throw ThrowHelper.InvalidOperationException($"Cannot project element of type {item.Type} in JSON array");
					}
				}
			}
			return array;
		}

		#region Flatten...

		/// <summary>Applatit une liste de liste en une liste tout court (équivalent d'un SelectMany récursif)</summary>
		/// <param name="deep">Si false, n'applatit que sur un niveau. Si true, aplatit récursivement quelque soit la profondeur</param>
		/// <returns>Liste applatie</returns>
		/// <example>
		/// Flatten([ [1,2], 3, [4, [5,6]] ], deep: false) => [ 1, 2, 3, 4, [5, 6] ]
		/// Flatten([ [1,2], 3, [4, [5,6]] ], deep: true) => [ 1, 2, 3, 4, 5, 6 ]
		/// </example>
		public JsonArray Flatten(bool deep = false)
		{
			var array = new JsonArray();
			FlattenRecursive(this, array, deep ? int.MaxValue : 1);
			return array;
		}

		private static void FlattenRecursive(JsonArray items, JsonArray output, int limit)
		{
			Contract.Debug.Requires(items != null && output != null);
			foreach(var item in items.AsSpan())
			{
				if (limit > 0 && item is JsonArray arr)
				{
					Contract.Debug.Requires(!ReferenceEquals(arr, items));
					FlattenRecursive(arr, output, limit - 1);
				}
				else
				{
					output.Add(item);
				}
			}
		}

		#endregion

		#region Errors...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception Error_CannotMapNullValuesToDictionary(int index)
		{
			return new InvalidOperationException($"Cannot map a JSON Array containing null value (at offset {index}) into a Dictionary");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception Error_CannotMapValueTypeToDictionary(JsonValue item, int index)
		{
			return new InvalidOperationException($"Cannot map a JSON Array containing a value of type {item.Type} (at offset {index}) into a Dictionary");
		}

		#endregion

		#region IJsonSerializable

		private static bool ShouldInlineArray(ReadOnlySpan<JsonValue> items)
		{
			switch (items.Length)
			{
				case 0: return true;
				case 1: return items[0].IsInlinable();
				case 2: return items[0].IsInlinable() && items[1].IsInlinable();
				default: return false;
			}
		}

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			var items = GetSpan();
			if (items.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			writer.MarkVisited(this);
			bool inline = writer.Indented && ShouldInlineArray(items);
			if (inline)
			{
				var state = writer.BeginInlineArray();
				writer.WriteInlineHeadSeparator();
				items[0].JsonSerialize(writer);
				for (int i = 1; i < items.Length; i++)
				{
					writer.WriteInlineTailSeparator();
					items[i].JsonSerialize(writer);
				}
				writer.EndInlineArray(state);
			}
			else
			{
				var state = writer.BeginArray();
				writer.WriteHeadSeparator();
				items[0].JsonSerialize(writer);
				for (int i = 1; i < items.Length; i++)
				{
					writer.WriteTailSeparator();
					items[i].JsonSerialize(writer);
				}
				writer.EndArray(state);
			}
			writer.Leave(this);
		}

		#endregion

		#region IEquatable<...>

		public override bool Equals(JsonValue? other) => other is JsonArray arr && Equals(arr);

		public bool Equals(JsonArray? other)
		{
			int n = m_size;
			if (other == null || other.Count != n)
			{
				return false;
			}
			var l = m_items;
			var r = other.m_items;
			for (int i = 0; i < n; i++)
			{
				if (!l[i].Equals(r[i]))
				{
					return false;
				}
			}
			return true;
		}

		public bool Equals(JsonArray? other, IEqualityComparer<JsonValue>? comparer)
		{
			int n = m_size;
			if (other == null || other.Count != n)
			{
				return false;
			}

			comparer ??= JsonValueComparer.Default;
			var l = m_items;
			var r = other.m_items;
			for (int i = 0; i < n; i++)
			{
				if (!comparer.Equals(l[i], r[i]))
				{
					return false;
				}
			}
			return true;
		}

		public override int GetHashCode()
		{
			// le hashcode de l'objet ne doit pas changer meme s'il est modifié (sinon on casse les hashtables!)
			return RuntimeHelpers.GetHashCode(this);

			//TODO: si on jour on gère les Read-Only Arrays, on peut utiliser ce code
			//int n = Math.Min(m_size, 4);

			//var items = m_items;
			//int h = 17;
			//for (int i = 0; i < n; i++)
			//{
			//	h = (h * 31) + items[i].GetHashCode();
			//}
			//h ^= m_size;
			//return h;
		}

		public override int CompareTo(JsonValue? other)
		{
			if (other is JsonArray jarr) return CompareTo(jarr);
			return base.CompareTo(other);
		}

		public int CompareTo(JsonArray other)
		{
			int n = Math.Min(m_size, other.m_size);
			var l = m_items;
			var r = other.m_items;
			for (int i = 0; i < n;i++)
			{
				int c = l[i].CompareTo(r[i]);
				if (c != 0) return c;
			}
			return m_size - other.m_size;
		}

		#endregion

		#region ISliceSerializable

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteByte('[');
			bool first = true;
			foreach (var item in this.AsSpan())
			{
				if (first)
				{
					first = false;
				}
				else
				{
					writer.WriteByte(',');
				}

				item.WriteTo(ref writer);
			}
			writer.WriteByte(']');
		}

		#endregion

	}

	[JetBrains.Annotations.PublicAPI]
	public static class JsonArrayExtensions
	{

		[Pure]
		public static TValue[] ToArray<TValue>(this JsonArray self, [InstantHandle] Func<JsonValue, TValue> transform)
		{
			Contract.NotNull(self);
			Contract.NotNull(transform);

			var items = self.AsSpan();
			var buf = new TValue[items.Length];

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = transform(items[i]);
			}

			return buf;
		}

		#region ToJsonArray...

		#region Mutable...

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this ReadOnlySpan<JsonValue> source)
			=> new JsonArray().AddRange(source!);

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this Span<JsonValue> source)
			=> new JsonArray().AddRange(source!);

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this JsonValue[] source)
			=> new JsonArray().AddRange(source);

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this IEnumerable<JsonValue?> source)
			=> new JsonArray().AddRange(source);

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>([InstantHandle] this IEnumerable<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues<TElement>(source, settings, resolver);

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>(this ReadOnlySpan<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues<TElement>(source, settings, resolver);

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>(this TElement[] source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues<TElement>(source, settings, resolver);

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArray<TSource>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, JsonValue?> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues(source, selector, settings, resolver);

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArray<TSource, TValue>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues(source, selector, settings, resolver);

		#endregion

		#region Immutable...

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this ReadOnlySpan<JsonValue> source)
			=> new JsonArray().AddRangeReadOnly(source!).FreezeUnsafe();

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this Span<JsonValue> source)
			=> new JsonArray().AddRangeReadOnly(source!).FreezeUnsafe();

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this JsonValue[] source)
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this IEnumerable<JsonValue?> source)
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly<TElement>(this IEnumerable<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly<TElement>(source, settings, resolver).FreezeUnsafe();

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly<TElement>(this ReadOnlySpan<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly<TElement>(source, settings, resolver).FreezeUnsafe();

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly<TElement>(this TElement[] source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly<TElement>(source, settings, resolver).FreezeUnsafe();

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArrayReadOnly<TSource>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, JsonValue?> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly(source, selector, settings, resolver).FreezeUnsafe();

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArrayReadOnly<TSource, TValue>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly(source, selector, settings, resolver).FreezeUnsafe();

		#endregion

		#endregion

		#region Pick...

		/// <summary>Retourne une liste d'objets copiés, et filtrés pour ne contenir que les champs autorisés dans <param name="keys"/>.</summary>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, ReadOnlySpan<string> keys, bool keepMissing = false)
		{
			Contract.NotNull(source);

			return JsonArray.Project(source, JsonObject.CheckProjectionFields(keys, keepMissing));
		}

		/// <summary>Retourne une liste d'objets copiés, et filtrés pour ne contenir que les champs autorisés dans <param name="keys"/>.</summary>
		/// <param name="source"></param>
		/// <param name="keys">Liste des clés à conserver sur les éléments de la liste</param>
		/// <param name="keepMissing">Si true, toute propriété manquante sera ajoutée avec la valeur 'null' / JsonNull.Missing.</param>
		/// <returns>Nouvelle liste contenant le résultat de la projection sur chaque élément de la liste</returns>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, IEnumerable<string> keys, bool keepMissing = false)
		{
			Contract.NotNull(source);
			Contract.NotNull(keys);

			return JsonArray.Project(source, JsonObject.CheckProjectionFields(keys as string[] ?? keys.ToArray(), keepMissing));
		}

		/// <summary>Retourne une liste d'objets copiés, et filtrés pour ne contenir que les champs autorisés dans <param name="defaults"/>.</summary>
		/// <param name="source"></param>
		/// <param name="defaults">Liste des clés à conserver sur les éléments de la liste</param>
		/// <returns>Nouvelle liste contenant le résultat de la projection sur chaque élément de la liste</returns>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, JsonObject defaults)
		{
			Contract.NotNull(source);
			return JsonArray.Project(source, JsonObject.CheckProjectionDefaults(defaults!));
		}

		/// <summary>Retourne une liste d'objets copiés, et filtrés pour ne contenir que les champs autorisés dans <param name="defaults"/>.</summary>
		/// <param name="source"></param>
		/// <param name="defaults">Liste des clés à conserver sur les éléments de la liste</param>
		/// <returns>Nouvelle liste contenant le résultat de la projection sur chaque élément de la liste</returns>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, IDictionary<string, JsonValue?> defaults)
		{
			Contract.NotNull(source);
			return JsonArray.Project(source, JsonObject.CheckProjectionDefaults(defaults));
		}

		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, object defaults)
		{
			Contract.NotNull(source);

			return JsonArray.Project(source, JsonObject.CheckProjectionDefaults(defaults));
		}

		#endregion

		/// <summary>Map les éléments de cette liste vers un dictionnaire</summary>
		/// <typeparam name="TKey">Type des clé du dictionnaire cible</typeparam>
		/// <typeparam name="TValue">Type des valeurs du dictionnaire cible</typeparam>
		/// <param name="source"></param>
		/// <param name="target">Dictionnaire cible dans lequel stocker les valeurs</param>
		/// <param name="expectedType">Type attendu des objets de la liste (ou null si aucun filtrage)</param>
		/// <param name="keySelector">Lambda utilisée pour extraire les clés du dictionnaire</param>
		/// <param name="valueSelector">Lambda utilisée pour extraire les valeurs stockées dans le dictionnaire</param>
		/// <param name="resolver">Résolveur de type custom (ou celui par défaut si null)</param>
		/// <param name="overwrite">Si true, écrase toute valeur existante. Si false, provoque une exception en cas de doublon de clé</param>
		/// <returns>L'annuaire cible passé en paramètre</returns>
		/// <exception cref="System.ArgumentNullException">Si l'un des paramètre requis est null</exception>
		/// <exception cref="System.InvalidOperationException">Si un élément de l'array est null, ou s'il n'est pas dy type attendu</exception>
		/// <remarks>Attention, l'Array ne doit pas contenir de valeur null !</remarks>
		public static IDictionary<TKey, TValue> MapTo<TKey, TValue>(
			[InstantHandle] this IEnumerable<JsonValue?> source,
			IDictionary<TKey, TValue> target,
			JsonType? expectedType,
			[InstantHandle] Func<JsonValue, JsonValue> keySelector,
			[InstantHandle] Func<JsonValue, JsonValue> valueSelector,
			ICrystalJsonTypeResolver? resolver = null,
			bool overwrite = false
		) where TKey : notnull
		{
			Contract.NotNull(source);
			Contract.NotNull(target);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			resolver ??= CrystalJson.DefaultResolver;

			int index = 0;
			foreach (var item in source)
			{
				if (item == null || item.IsNull)
				{
					throw JsonArray.Error_CannotMapNullValuesToDictionary(index);
				}
				if (expectedType.HasValue && item.Type != expectedType.GetValueOrDefault())
				{
					throw JsonArray.Error_CannotMapValueTypeToDictionary(item, index);
				}

				var key = keySelector(item).Required<TKey>(resolver);
				var value = valueSelector(item).As<TValue>(resolver)!;
				if (overwrite)
				{
					target[key] = value;
				}
				else
				{
					target.Add(key, value);
				}
				++index;
			}

			return target;
		}

	}

	/// <summary>Wrapper for a <see cref="JsonArray"/> that casts each element into a <typeparamref name="TJson"/>.</summary>
	public readonly struct JsonArray<TJson> : IReadOnlyList<TJson>
		where TJson : JsonValue
	{
		//note: this is to convert JsonValue into JsonArray, JsonObject, JsonType, ...

		private readonly JsonValue[] m_items;
		private readonly int m_size;
		private readonly bool m_required;

		internal JsonArray(JsonValue[] items, int size, bool required)
		{
			m_items = items;
			m_size = size;
			m_required = required;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator() => new(m_items, m_size, m_required);

		IEnumerator<TJson> IEnumerable<TJson>.GetEnumerator() => new Enumerator(m_items, m_size, m_required);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(m_items, m_size, m_required);

		/// <summary>Enumerator qui cast chaque élément d'une <see cref="JsonArray"/> en un <b>TJson</b>.</summary>
		public struct Enumerator : IEnumerator<TJson>
		{
			private readonly JsonValue[] m_items;
			private readonly int m_size;
			private int m_index;
			private TJson? m_current;
			private readonly bool m_required;

			internal Enumerator(JsonValue[] items, int size, bool required)
			{
				m_items = items;
				m_size = size;
				m_index = 0;
				m_current = default;
				m_required = required;
			}

			public void Dispose()
			{ }

			public bool MoveNext()
			{
				//TODO: check versioning?
				if ((uint) m_index < (uint) m_size)
				{
					if (m_items[m_index] is not TJson val)
					{ // null ou autre type de Json
						return MoveNextNullOrInvalidCast();
					}
					m_current = val;
					m_index++;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextNullOrInvalidCast()
			{
				// appelé si le cast "as JsonValue" en T retourne null:
				// - car la valeur est un JsonNull (dans ce cas on retourne un null)
				// - car la valeur est un autre type de Json que celui attendu
				int index = m_index;
				var val = m_items[index];
				if (!val.IsNullOrMissing())
				{ // pas compatible
					throw FailElementAtIndexCannotBeConverted(index, val);
				}

				if (m_required)
				{
					throw FailElementAtIndexNullOrMissing(index);
				}

				//note: on considère que c'est l'équivalent de "foreach(string s in new [] { "hello", null, "world" }) { ... }"
				// => dans ce cas, s vaudrait aussi 'null'. C'est la responsabilité de l'appelant de se débrouiller avec!
				m_current = null;
				m_index = index + 1;
				return true;
			}

			private bool MoveNextRare()
			{
				//TODO: check versioning?
				m_index = m_size + 1;
				m_current = default;
				return false;
			}

			object IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index == m_size + 1)
					{
						throw ThrowHelper.InvalidOperationException("Operation cannot happen.");
					}
					return this.Current;
				}
			}

			void IEnumerator.Reset()
			{
				//TODO: check versioning?
				m_index = 0;
				m_current = default;
			}

			public TJson Current => m_current!;
		}

		public int Count => m_size;

		public TJson this[int index]
		{
			get
			{
				// Following trick can reduce the range check by one
				if ((uint) index >= (uint) m_size) throw ThrowHelper.ArgumentOutOfRangeIndex(index);
				//REVIEW: support negative indexing ?
				return (m_items[index] as TJson) ?? GetNextNullOrInvalid(index)!;
			}
		}

		public TJson this[Index index] => this[index.GetOffset(m_size)];

		private TJson? GetNextNullOrInvalid(int index)
		{
			var item = m_items[index];
			if (!item.IsNullOrMissing()) throw FailElementAtIndexCannotBeConverted(index, item);
			if (m_required) throw FailElementAtIndexNullOrMissing(index);
			return null;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidCastException FailElementAtIndexCannotBeConverted(int index, JsonValue val) => new($"The JSON element at index {index} contains a {val.GetType().Name} that cannot be converted into a {typeof(TJson).Name}");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailElementAtIndexNullOrMissing(int index) => new($"The JSON element at index {index} is null or missing");

		[Pure]
		public TJson[] ToArray()
		{
			if (m_size == 0) return [];
			var res = new TJson[m_size];
			int p = 0;
			foreach (var item in this)
			{
				res[p++] = item;
			}
			if (p != res.Length) throw new InvalidOperationException();
			return res;
		}

		[Pure]
		public TValue[] ToArray<TValue>([InstantHandle] Func<TJson, TValue> transform)
		{
			Contract.NotNull(transform);

			if (m_size == 0) return [];
			var res = new TValue[m_size];
			int p = 0;
			foreach (var item in this)
			{
				res[p++] = transform(item);
			}
			if (p != res.Length) throw new InvalidOperationException();
			return res;
		}

		[Pure]
		public List<TJson> ToList()
		{
			var res = new List<TJson>(m_size);
			foreach(var item in this)
			{
				res.Add(item);
			}
			if (m_size != res.Count) throw new InvalidOperationException();
			return res;
		}

		[Pure]
		public List<TValue> ToList<TValue>([InstantHandle] Func<TJson, TValue> transform)
		{
			var res = new List<TValue>(m_size);
			foreach (var item in this)
			{
				res.Add(transform(item));
			}
			if (m_size != res.Count) throw new InvalidOperationException();
			return res;
		}

	}

}
