#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
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
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Array of JSON values</summary>
	[Serializable]
	[DebuggerDisplay("JSON Array[{m_size}] {GetCompactRepresentation(0),nq}")]
	[DebuggerTypeProxy(typeof(JsonArray.DebugView))]
	[DebuggerNonUserCode]
	public sealed class JsonArray : JsonValue, IList<JsonValue>, IReadOnlyList<JsonValue>, IEquatable<JsonArray>
	{
		/// <summary>Taille initiale de l'array</summary>
		private const int DEFAULT_CAPACITY = 4;
		/// <summary>Capacité maximale pour conserver le buffer en cas de clear</summary>
		private const int MAX_KEEP_CAPACITY = 1024;
		/// <summary>Capacité maximale pour la croissance automatique</summary>
		private const int MAX_GROWTH_CAPACITY = 0X7FEFFFFF;

		//TODO: OPTIMIZE: quelle intial_size et growth_ratio?

		// => .NET démarre a 4 et utilise n*2 ce qui minimise le cout d'une insertion, mais maximize la consommation mémoire
		//    > [4], 8, 16, 32, 64, 128, 256, 512, 1024, 2048, ...
		//	  -    3 (  24 bytes) =>    4 slots (  32 bytes) =>   1 waste (   8 bytes) =>    3 writes (   24 bytes) =>  1 allocations,  0 resize
		//	  -   10 (  80 bytes) =>   16 slots ( 128 bytes) =>   6 waste (  48 bytes) =>   22 writes (  176 bytes) =>  3 allocations,  2 resize
		//	  -   31 ( 248 bytes) =>   32 slots ( 256 bytes) =>   1 waste (   8 bytes) =>   59 writes (  472 bytes) =>  4 allocations,  3 resize
		//	  -   60 ( 480 bytes) =>   64 slots ( 512 bytes) =>   4 waste (  32 bytes) =>  120 writes (  960 bytes) =>  5 allocations,  4 resize
		//	  -  100 ( 800 bytes) =>  128 slots (1024 bytes) =>  28 waste ( 224 bytes) =>  224 writes ( 1792 bytes) =>  6 allocations,  5 resize
		//	  -  366 (2928 bytes) =>  512 slots (4096 bytes) => 146 waste (1168 bytes) =>  874 writes ( 6992 bytes) =>  8 allocations,  7 resize
		//	  - 1000 (8000 bytes) => 1024 slots (8192 bytes) =>  24 waste ( 192 bytes) => 2020 writes (16160 bytes) =>  9 allocations,  8 resize

		// => JAVA démarre a 10 et utilise (n*3/2)+1 (+50%), a savoir si ca nous donne le meilleur compromis, ou un sidecar :) (1.7 fois plus de resize de .NET, pour 2x moins de waste de mémoire en moyenne)
		//    > [10], 16, 25, 38, 58, 88, 133, 200, 301, 452, 679, 1019, 1529, 2294, ...
		//	  -    3 (  24 bytes) =>   10 slots (  80 bytes) =>   7 waste (  56 bytes) =>    3 writes (   24 bytes) =>  1 allocations,  0 resize
		//	  -   10 (  80 bytes) =>   10 slots (  80 bytes) =>   0 waste (   0 bytes) =>   10 writes (   80 bytes) =>  1 allocations,  0 resize
		//	  -   31 ( 248 bytes) =>   38 slots ( 304 bytes) =>   7 waste (  56 bytes) =>   82 writes (  656 bytes) =>  4 allocations,  3 resize
		//	  -   60 ( 480 bytes) =>   88 slots ( 704 bytes) =>  28 waste ( 224 bytes) =>  207 writes ( 1656 bytes) =>  6 allocations,  5 resize
		//	  -  100 ( 800 bytes) =>  133 slots (1064 bytes) =>  33 waste ( 264 bytes) =>  335 writes ( 2680 bytes) =>  7 allocations,  6 resize
		//	  -  366 (2928 bytes) =>  452 slots (3616 bytes) =>  86 waste ( 688 bytes) => 1235 writes ( 9880 bytes) => 10 allocations,  9 resize
		//	  - 1000 (8000 bytes) => 1019 slots (8152 bytes) =>  19 waste ( 152 bytes) => 3000 writes (24000 bytes) => 12 allocations, 11 resize

		// => CPython démarre a 4 utilise n*9/8 (+12.5%)) avec de l'aide au démarrage ce qui minimise la consommation mémoire, mais augmente le nombre de resize (6 fois plus de resize que .NET environ, mais 8x moins de waste mémoire en moyenne)
		//    > [4], 8, 16, 25, 35, 46, 58, 72, 88, 106, 126, 148, 173, 201, 233, 269, 309, 354, 405, 462, 526, 598, 679, 771, 874, 990, 1120, 1267, 1432, 1618, 1827, 2062, ...
		//	  -    3 (  24 bytes) =>    4 slots (  32 bytes) =>   1 waste (   8 bytes) =>    3 writes (   24 bytes) =>  1 allocations,  0 resize
		//	  -   10 (  80 bytes) =>   16 slots ( 128 bytes) =>   6 waste (  48 bytes) =>   22 writes (  176 bytes) =>  3 allocations,  2 resize
		//	  -   31 ( 248 bytes) =>   35 slots ( 280 bytes) =>   4 waste (  32 bytes) =>   84 writes (  672 bytes) =>  5 allocations,  4 resize
		//	  -   60 ( 480 bytes) =>   72 slots ( 576 bytes) =>  12 waste (  96 bytes) =>  252 writes ( 2016 bytes) =>  8 allocations,  7 resize
		//	  -  100 ( 800 bytes) =>  106 slots ( 848 bytes) =>   6 waste (  48 bytes) =>  452 writes ( 3616 bytes) => 10 allocations,  9 resize
		//	  -  366 (2928 bytes) =>  405 slots (3240 bytes) =>  39 waste ( 312 bytes) => 2637 writes (21096 bytes) => 19 allocations, 18 resize
		//	  - 1000 (8000 bytes) => 1120 slots (8960 bytes) => 120 waste ( 960 bytes) => 8576 writes (68608 bytes) => 27 allocations, 26 resize

		private JsonValue?[] m_items; // 8 + sizeof(ARRAY_HEADER) + capacity * 8
		private int m_size; // 4
		//note: on a encore 4 bytes de padding utilisable !

		// Memory Footprint:
		// -----------------
		// - JsonArray   : this=16 + m_items=8 + m_size=4 + (pad)=4  = 32 bytes
		// - JsonValue[] : this=32 + capacity * 8  = 32 + 8*capacity
		// > Total: 64 bytes + capacity * 8 bytes
		//      0 ..     4 entries =>    96 bytes
		//      5 ..     8 entries =>   128 bytes
		//      9 ..    16 entries =>   192 bytes
		//     17 ..    32 entries =>   320 bytes
		//     33 ..    64 entries =>   576 bytes
		//     65 ..   128 entries => 1,088 bytes
		//    129 ..   256 entries => 2,112 bytes
		//    257 ..   512 entries => 4,160 bytes
		//    513 .. 1,024 entries => 8,256 bytes

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private class DebugView
		{
			private readonly JsonArray m_array;

			public DebugView(JsonArray array)
			{
				m_array = array;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public JsonValue[] Items
			{
				get { return m_array.ToArray(); }
			}
		}

		#region Constructors...

		/// <summary>Retourne une nouvelle array vide</summary>
		public static JsonArray Empty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new JsonArray(0);
		}

		/// <summary>Crée une nouvelle array vide</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray()
		{
			m_items = Array.Empty<JsonValue?>();
		}

		/// <summary>Crée une nouvelle array avec une capacité initiale</summary>
		/// <param name="capacity">Capacité initiale</param>
		public JsonArray(int capacity)
		{
			if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(capacity), "Non-negative number required.");

			m_items = capacity == 0 ? Array.Empty<JsonValue?>() : new JsonValue?[capacity];
		}

		/// <summary>Create a new JsonArray, and fill it with a sequence of elements</summary>
		/// <param name="collection">Sequence of elements that will be copied to the array</param>
		public JsonArray(IEnumerable<JsonValue> collection)
		{
			Contract.NotNull(collection);

			//note: optimisé car fréquemment utilisé lorsqu'on manipule des array JSON (via LINQ par exemple)

			switch (collection)
			{
				case JsonArray j:
				{ // copy directly from the source after resizing
					int count = j.m_size;
					if (count == 0)
					{
						m_items = Array.Empty<JsonValue?>();
					}
					else
					{
						var items = j.m_items.AsSpan(0, count).ToArray();
						FillNullValues(items);
						m_items = items;
						m_size = count;
					}
					break;
				}

				case JsonValue[] arr:
				{ // copy directly after resizing
					int count = arr.Length;
					if (count == 0)
					{
						m_items = Array.Empty<JsonValue?>();
					}
					else
					{
						var items = arr.AsSpan(0, count).ToArray();
						FillNullValues(items);
						m_items = items;
						m_size = count;
					}
					break;
				}

				case ICollection<JsonValue> c:
				{
					int count = c.Count;
					if (count == 0)
					{
						m_items = Array.Empty<JsonValue>();
					}
					else
					{
						m_items = new JsonValue?[count];
						c.CopyTo(m_items!, 0);
						FillNullValues(m_items);
						m_size = count;
					}
					break;
				}

				default:
				{
					m_items = Array.Empty<JsonValue?>();
					using (var en = collection.GetEnumerator())
					{
						while (en.MoveNext()) Add(en.Current);
					}
					break;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonArray(JsonValue?[] items, int count)
		{
			Contract.Debug.Requires(items != null && count <= items.Length);
			m_items = items;
			m_size = count;
#if DEBUG
			for (int i = 0; i < count; i++) Contract.Debug.Requires(items[i] != null, "Array cannot contain nulls");
#endif
		}

		[Pure]
		public static JsonArray CopyFrom(ReadOnlySpan<JsonValue> items)
		{
			if (items.Length == 0) return new JsonArray();

			var local = new JsonValue[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				local[i] = items[i] ?? throw ThrowHelper.InvalidOperationException($"Internal JSON array cannot contain null value at index {i}.");
			}
			return new JsonArray(local, items.Length);
		}

		/// <summary>Remplace toute occurence de null par JsonNull.Null</summary>
		private static void FillNullValues(Span<JsonValue?> items)
		{
			for (int i = 0; i < items.Length; i++)
			{
				items[i] ??= JsonNull.Null;
			}
		}

		/// <summary>Create a new JsonArray that will hold a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue value)
		{
			Contract.Debug.Requires(value != null);
			return new JsonArray(new [] { value }, 1);
		}

		/// <summary>Create a new JsonArray that will hold a pair of elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue value1, JsonValue value2)
		{
			Contract.Debug.Requires(value1 != null && value2 != null);
			return new JsonArray(new [] { value1, value2 }, 2);
		}

		/// <summary>Create a new JsonArray that will hold three elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue value1, JsonValue value2, JsonValue value3)
		{
			Contract.Debug.Requires(value1 != null && value2 != null && value3 != null);
			return new JsonArray(new [] { value1, value2, value3 }, 3);
		}

		/// <summary>Create a new JsonArray that will hold four elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(JsonValue value1, JsonValue value2, JsonValue value3, JsonValue value4)
		{
			Contract.Debug.Requires(value1 != null && value2 != null && value3 != null && value4 != null);
			return new JsonArray(new [] { value1, value2, value3, value4 }, 4);
		}

		/// <summary>Create a new JsonArray using a list of elements</summary>
		/// <param name="values">Elements of the new array</param>
		/// <remarks>The JSON array returned will keep a reference on the <paramref name="values"/> array. Any changes made to it will be visible on the JSON Array!
		/// Use <see cref="CopyFrom(System.ReadOnlySpan{Doxense.Serialization.Json.JsonValue})"/> if you want to <i>copy</i> the items!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Create(params JsonValue[] values)
		{
			Contract.NotNull(values);
			return values.Length > 0 ? new JsonArray(values, values.Length) : new JsonArray();
		}

		/// <summary>Combine two JsonArrays into a single new array</summary>
		/// <remarks>The new array is contain a copy of the items of the two input arrays</remarks>
		public static JsonArray Combine(JsonArray arr1, JsonArray arr2)
		{
			int size1 = arr1.m_size;
			int size2 = arr2.m_size;
			var tmp = new JsonValue?[checked(size1 + size2)];
			arr1.m_items.AsSpan(0, size1).CopyTo(tmp);
			arr2.m_items.AsSpan(0, size2).CopyTo(tmp.AsSpan(size1));
			return new JsonArray(tmp, size1 + size2);
		}

		/// <summary>Combine three JsonArrays into a single new array</summary>
		/// <remarks>The new array is contain a copy of the items of the three input arrays</remarks>
		public static JsonArray Combine(JsonArray arr1, JsonArray arr2, JsonArray arr3)
		{
			int size1 = arr1.m_size;
			int size2 = arr2.m_size;
			int size3 = arr3.m_size;
			var tmp = new JsonValue?[checked(size1 + size2 + size3)];
			arr1.m_items.AsSpan(0, size1).CopyTo(tmp);
			arr2.m_items.AsSpan(0, size2).CopyTo(tmp.AsSpan(size1));
			arr3.m_items.AsSpan(0, size3).CopyTo(tmp.AsSpan(size1 + size2));
			return new JsonArray(tmp, size1 + size2 + size3);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une séquence d'éléments dont le type est connu.</summary>
		/// <typeparam name="T">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Séquences d'éléments à convertir</param>
		/// <returns>JsonArray contenant tous les éléments de la séquence, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<T>(IEnumerable<T> values)
		{
			return new JsonArray().AddRange<T>(values);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une séquence d'éléments dont le type est connu.</summary>
		/// <typeparam name="TCollection">Type de base de la séquence</typeparam>
		/// <typeparam name="TElement">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Séquences d'éléments à convertir</param>
		/// <returns>JsonArray contenant tous les éléments de la séquence, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TCollection, TElement>(TCollection values)
			where TCollection : struct, ICollection<TElement>
		{
			var arr = new JsonArray(values.Count);
			foreach (var item in arr)
			{
				arr.Add(item);
			}
			return arr;
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une séquence d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments de la séquence</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="items">Séquences d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <returns>JsonArray contenant tous les valeurs de la séquence, convertis en JsonValue</returns>
		[Pure]
		[return: MaybeNull, NotNullIfNotNull("items")]
		public static JsonArray FromValues<TItem, TValue>(IEnumerable<TItem>? items, Func<TItem, TValue> selector)
		{
			Contract.NotNull(selector);
			if (items == null) return JsonArray.Empty;

			return new JsonArray().AddRange(items, selector);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'une liste d'éléments dont le type est connu.</summary>
		/// <typeparam name="T">Type de base des éléments de la liste</typeparam>
		/// <param name="values">Liste d'éléments à convertir</param>
		/// <returns>JsonArray contenant tous les éléments de la séquence, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<T>(List<T> values)
		{
			return new JsonArray().AddRange<T>(values);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableu d'éléments dont le type est connu.</summary>
		/// <typeparam name="T">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <returns>JsonArray contenant tous les éléments du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<T>(T[] values)
		{
			return new JsonArray().AddRange<T>(values);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableau d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments du tableau</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="items">Tableau d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <returns>JsonArray contenant tous les valeurs du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TItem, TValue>(TItem[]? items, Func<TItem, TValue> selector)
		{
			Contract.NotNull(selector);
			return items == null ? JsonArray.Empty : new JsonArray().AddRange(items.AsSpan(), selector);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableu d'éléments dont le type est connu.</summary>
		/// <typeparam name="T">Type de base des éléments de la séquence</typeparam>
		/// <param name="values">Tableau d'éléments à convertir</param>
		/// <returns>JsonArray contenant tous les éléments du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<T>(ReadOnlySpan<T> values)
		{
			return values.Length == 0 ? JsonArray.Empty : new JsonArray().AddRange<T>(values);
		}

		/// <summary>Crée une nouvelle JsonArray à partir d'un tableau d'éléments dont le type est connu.</summary>
		/// <typeparam name="TItem">Type de base des éléments du tableau</typeparam>
		/// <typeparam name="TValue">Type des valeurs extraites de chaque élément, et insérée dans la JsonArray</typeparam>
		/// <param name="items">Tableau d'éléments à convertir</param>
		/// <param name="selector">Lambda qui extrait une valeur d'un item</param>
		/// <returns>JsonArray contenant tous les valeurs du tableau, convertis en JsonValue</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TItem, TValue>(ReadOnlySpan<TItem> items, Func<TItem, TValue> selector)
		{
			Contract.NotNull(selector);
			return items.Length == 0 ? JsonArray.Empty : new JsonArray().AddRange(items, selector);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1>(ValueTuple<T1> tuple)
		{
			return new JsonArray(new [] { FromValue<T1>(tuple.Item1) }, 1);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2>(ValueTuple<T1, T2> tuple)
		{
			return new JsonArray(new [] { FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2) }, 2);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3>(ValueTuple<T1, T2, T3> tuple)
		{
			return new JsonArray(new [] { FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3) }, 3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> tuple)
		{
			return new JsonArray(new [] { FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4) }, 4);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> tuple)
		{
			return new JsonArray(new [] { FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5) }, 5);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5, T6>(ValueTuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return new JsonArray(new [] { FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5), FromValue<T6>(tuple.Item6) }, 6);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5, T6, T7>(ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			return new JsonArray(new[] { FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5), FromValue<T6>(tuple.Item6), FromValue<T7>(tuple.Item7) }, 7);
		}

		#endregion

		public override JsonType Type => JsonType.Array;

		/// <summary>Une Array ne peut pas être null</summary>
		public override bool IsNull
		{
			[ContractAnnotation("=> false")]
			get => false;
		}

		/// <summary>La valeur par défaut pour une array est null, donc retourne toujours false</summary>
		public override bool IsDefault
		{
			[ContractAnnotation("=> false")]
			get => false;
		}

		/// <summary>Indique si le tableau est vide</summary>
		public bool IsEmpty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_size == 0;
		}

		#region List<T>...

		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_size;
		}

		/// <summary>Fixe ou retourne la capacité interne de l'array</summary>
		/// <remarks>
		/// A utiliser avant d'insérer un grand nombre d'éléments si la taille finale est connue.
		/// Si la nouvelle capacité est inférieure au nombre d'items existants, une exception est générée.
		/// </remarks>
		public int Capacity
		{
			get => m_items.Length;
			set
			{
				if (value < m_size) throw ThrowHelper.ArgumentOutOfRangeException(nameof(value), "capacity was less than the current size.");
				if (value != m_items.Length)
				{
					ResizeBuffer(value);
				}
			}
		}

		/// <summary>Vérifie la capacité du buffer interne, et resize le ci besoin</summary>
		/// <param name="min">Taille minimum du buffer</param>
		private void EnsureCapacity(int min)
		{
			// devrait être inliné
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
				var tmp = new JsonValue?[size];
				// copie les anciennes valeurs
				if (m_size > 0) m_items.AsSpan(0, m_size).CopyTo(tmp);
				m_items = tmp;
			}
			else
			{
				m_items = Array.Empty<JsonValue?>();
			}
		}

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

		/// <inheritdoc cref="JsonValue.this[int]"/>
		public override JsonValue this[int index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				// Following trick can reduce the range check by one
				if ((uint)index >= (uint)m_size) ThrowHelper.ThrowArgumentOutOfRangeIndex(index);
				//TODO: REVIEW: support negative indexing ?
				return m_items[index] ?? JsonNull.Null;
			}
			set
			{
				Contract.Debug.Requires(!object.ReferenceEquals(this, value));
				if ((uint)index >= (uint)m_size) ThrowHelper.ThrowArgumentOutOfRangeIndex(index);
				//TODO: REVIEW: support negative indexing ?
				m_items[index] = value ?? JsonNull.Null;
			}
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Add(JsonValue value)
		{
			Contract.Debug.Requires(!object.ReferenceEquals(this, value));
			// invariant: adding 'null' will add JsonNull.Null
			int size = m_size;
			if (size == m_items.Length) EnsureCapacity(size + 1);
			m_items[size] = value ?? JsonNull.Null;
			m_size = size + 1;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddNull()
		{
			Add(JsonNull.Null);
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddValue<T>(T value)
		{
			Add(FromValue<T>(value));
		}

		#region AddRange...

		//note: AddRange(..) est appelé par beaucoup de helpers comme ToJsonArray(), CreateRange(), ....
		// => c'est ici qu'on doit centraliser toute la logique d'optimisations (pour éviter d'en retrouver partout)

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(JsonArray items)
		{
			return AddRange(items, deepCopy: false);
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(JsonValue[] items)
		{
			return AddRange(items.AsSpan(), deepCopy: false);
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(ReadOnlySpan<JsonValue> items)
		{
			return AddRange(items, deepCopy: false);
		}

		/// <summary>Append une autre séquence en copiant ses éléments à la fin</summary>
		/// <param name="items">Séquence à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(IEnumerable<JsonValue> items)
		{
			return AddRange(items, deepCopy: false);
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		internal JsonArray AddRangeBoxed(IEnumerable<object?> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			//note: internal pour éviter que ce soit trop facile de passer par 'object' au lieu de 'T'
			Contract.NotNull(items);

			// si c'est déjà une collection de JsonValue, on n'a pas besoin de les convertir
			if (items is IEnumerable<JsonValue> values)
			{
				AddRange(values);
				return this;
			}

			settings ??= CrystalJsonSettings.Json;
			resolver ??= CrystalJson.DefaultResolver;

			// pré-alloue si on connaît à l'avance la taille
			if (items is ICollection coll) EnsureCapacity(this.Count + coll.Count);

			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(JsonValue.FromValue(value, settings, resolver));
			}

			return this;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<T>(IEnumerable<T> items, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);

			if (CrystalJsonDomWriter.AreDefaultSettings(settings, resolver))
			{
				return AddRange<T>(items);
			}

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObject(value, typeof(T)));
			}
			return this;
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<T>(IEnumerable<T> items)
		{
			Contract.NotNull(items);

			{ // fast path pour des array
				if (items is T[] arr) return AddRange<T>(arr);
			}
			{ // fast path pour des listes
				if (items is List<T> list) return AddRange<T>(list);
			}
			{ // si c'est déja une collection de JsonValue, on n'a pas besoin de les convertir
				if (items is IEnumerable<JsonValue> json) return AddRange(json, deepCopy: false);
			}
			{ // pré-alloue si on connait à l'avance la taille
				if (items is ICollection<T> coll) EnsureCapacity(this.Count + coll.Count);
			}

			#region <JIT_HACK>
			// pattern reconnu et optimisé par le JIT en Release
#if !DEBUG
			if (typeof(T) == typeof(bool)
			 || typeof(T) == typeof(char)
			 || typeof(T) == typeof(byte)
			 || typeof(T) == typeof(sbyte)
			 || typeof(T) == typeof(short)
			 || typeof(T) == typeof(ushort)
			 || typeof(T) == typeof(int)
			 || typeof(T) == typeof(uint)
			 || typeof(T) == typeof(long)
			 || typeof(T) == typeof(ulong)
			 || typeof(T) == typeof(float)
			 || typeof(T) == typeof(double)
			 || typeof(T) == typeof(decimal)
			 || typeof(T) == typeof(Guid)
			 || typeof(T) == typeof(DateTime)
			 || typeof(T) == typeof(DateTimeOffset)
			 || typeof(T) == typeof(TimeSpan)
			 || typeof(T) == typeof(NodaTime.Instant)
			 || typeof(T) == typeof(NodaTime.Duration)
			 // nullables!
			 || typeof(T) == typeof(bool?)
			 || typeof(T) == typeof(char?)
			 || typeof(T) == typeof(byte?)
			 || typeof(T) == typeof(sbyte?)
			 || typeof(T) == typeof(short?)
			 || typeof(T) == typeof(ushort?)
			 || typeof(T) == typeof(int?)
			 || typeof(T) == typeof(uint?)
			 || typeof(T) == typeof(long?)
			 || typeof(T) == typeof(ulong?)
			 || typeof(T) == typeof(float?)
			 || typeof(T) == typeof(double?)
			 || typeof(T) == typeof(decimal?)
			 || typeof(T) == typeof(Guid?)
			 || typeof(T) == typeof(DateTime?)
			 || typeof(T) == typeof(DateTimeOffset?)
			 || typeof(T) == typeof(TimeSpan?)
			 || typeof(T) == typeof(NodaTime.Instant?)
			 || typeof(T) == typeof(NodaTime.Duration?)
			)
			{
				// version également optimisée!
				foreach (var item in items)
				{
					Add(FromValue<T>(item));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Default;
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(T);
			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, value, type, null));
			}
			return this;
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TInput">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TOutput">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<TInput, TOutput>(IEnumerable<TInput> items, Func<TInput, TOutput> transform)
		{
			Contract.NotNull(items);
			Contract.NotNull(transform);

			// il y a plus de chances que items soit une liste/array/collection (sauf s'il y a un Where(..) avant)
			// donc ca vaut le coup tenter de pré-allouer l'array
			if (items is ICollection<TInput> coll)
			{
				EnsureCapacity(this.Count + coll.Count);
			}

			#region <JIT_HACK>
			// pattern reconnu et optimisé par le JIT en Release
#if !DEBUG
			if (typeof(TOutput) == typeof(bool)
			 || typeof(TOutput) == typeof(char)
			 || typeof(TOutput) == typeof(byte)
			 || typeof(TOutput) == typeof(sbyte)
			 || typeof(TOutput) == typeof(short)
			 || typeof(TOutput) == typeof(ushort)
			 || typeof(TOutput) == typeof(int)
			 || typeof(TOutput) == typeof(uint)
			 || typeof(TOutput) == typeof(long)
			 || typeof(TOutput) == typeof(ulong)
			 || typeof(TOutput) == typeof(float)
			 || typeof(TOutput) == typeof(double)
			 || typeof(TOutput) == typeof(decimal)
			 || typeof(TOutput) == typeof(Guid)
			 || typeof(TOutput) == typeof(DateTime)
			 || typeof(TOutput) == typeof(DateTimeOffset)
			 || typeof(TOutput) == typeof(TimeSpan)
			 || typeof(TOutput) == typeof(NodaTime.Instant)
			 || typeof(TOutput) == typeof(NodaTime.Duration)
			 // nullables!
			 || typeof(TOutput) == typeof(bool?)
			 || typeof(TOutput) == typeof(char?)
			 || typeof(TOutput) == typeof(byte?)
			 || typeof(TOutput) == typeof(sbyte?)
			 || typeof(TOutput) == typeof(short?)
			 || typeof(TOutput) == typeof(ushort?)
			 || typeof(TOutput) == typeof(int?)
			 || typeof(TOutput) == typeof(uint?)
			 || typeof(TOutput) == typeof(long?)
			 || typeof(TOutput) == typeof(ulong?)
			 || typeof(TOutput) == typeof(float?)
			 || typeof(TOutput) == typeof(double?)
			 || typeof(TOutput) == typeof(decimal?)
			 || typeof(TOutput) == typeof(Guid?)
			 || typeof(TOutput) == typeof(DateTime?)
			 || typeof(TOutput) == typeof(DateTimeOffset?)
			 || typeof(TOutput) == typeof(TimeSpan?)
			 || typeof(TOutput) == typeof(NodaTime.Instant?)
			 || typeof(TOutput) == typeof(NodaTime.Duration?)
			)
			{
				// version également optimisée!
				foreach (var item in items)
				{
					Add(FromValue<TOutput>(transform(item)));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Default;
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TOutput);
			foreach (var item in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, transform(item), type, null));
			}
			return this;
		}

		/// <summary>Ajout le résultat de la transformation des éléments d'une séquence</summary>
		/// <typeparam name="TInput">Type des éléments de <paramref name="items"/></typeparam>
		/// <typeparam name="TOutput">Type du résultat transformé</typeparam>
		/// <param name="items">Séquence d'éléments d'origine</param>
		/// <param name="transform">Transformation appliquée à chaque élément</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<TInput, TOutput>(ReadOnlySpan<TInput> items, Func<TInput, TOutput> transform)
		{
			Contract.NotNull(transform);

			// il y a plus de chances que items soit une liste/array/collection (sauf s'il y a un Where(..) avant)
			// donc ca vaut le coup tenter de pré-allouer l'array
			EnsureCapacity(checked(this.Count + items.Length));

			#region <JIT_HACK>
			// pattern reconnu et optimisé par le JIT en Release
#if !DEBUG
			if (typeof(TOutput) == typeof(bool)
			 || typeof(TOutput) == typeof(char)
			 || typeof(TOutput) == typeof(byte)
			 || typeof(TOutput) == typeof(sbyte)
			 || typeof(TOutput) == typeof(short)
			 || typeof(TOutput) == typeof(ushort)
			 || typeof(TOutput) == typeof(int)
			 || typeof(TOutput) == typeof(uint)
			 || typeof(TOutput) == typeof(long)
			 || typeof(TOutput) == typeof(ulong)
			 || typeof(TOutput) == typeof(float)
			 || typeof(TOutput) == typeof(double)
			 || typeof(TOutput) == typeof(decimal)
			 || typeof(TOutput) == typeof(Guid)
			 || typeof(TOutput) == typeof(DateTime)
			 || typeof(TOutput) == typeof(DateTimeOffset)
			 || typeof(TOutput) == typeof(TimeSpan)
			 || typeof(TOutput) == typeof(NodaTime.Instant)
			 || typeof(TOutput) == typeof(NodaTime.Duration)
			 // nullables!
			 || typeof(TOutput) == typeof(bool?)
			 || typeof(TOutput) == typeof(char?)
			 || typeof(TOutput) == typeof(byte?)
			 || typeof(TOutput) == typeof(sbyte?)
			 || typeof(TOutput) == typeof(short?)
			 || typeof(TOutput) == typeof(ushort?)
			 || typeof(TOutput) == typeof(int?)
			 || typeof(TOutput) == typeof(uint?)
			 || typeof(TOutput) == typeof(long?)
			 || typeof(TOutput) == typeof(ulong?)
			 || typeof(TOutput) == typeof(float?)
			 || typeof(TOutput) == typeof(double?)
			 || typeof(TOutput) == typeof(decimal?)
			 || typeof(TOutput) == typeof(Guid?)
			 || typeof(TOutput) == typeof(DateTime?)
			 || typeof(TOutput) == typeof(DateTimeOffset?)
			 || typeof(TOutput) == typeof(TimeSpan?)
			 || typeof(TOutput) == typeof(NodaTime.Instant?)
			 || typeof(TOutput) == typeof(NodaTime.Duration?)
			)
			{
				// version également optimisée!
				foreach (var item in items)
				{
					Add(FromValue<TOutput>(transform(item)));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Default;
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TOutput);
			foreach (var item in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, transform(item), type, null));
			}
			return this;
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<T>(params T[] items)
		{
			Contract.NotNull(items);
			return AddRange<T>(items.AsSpan());
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<T>(ReadOnlySpan<T> items)
		{
			if (items.Length == 0) return this;

			// pré-alloue si on connait à l'avance la taille
			EnsureCapacity(this.Count + items.Length);

			#region <JIT_HACK>
			// pattern reconnu et optimisé par le JIT en Release
#if !DEBUG
			if (typeof(T) == typeof(bool)
			 || typeof(T) == typeof(char)
			 || typeof(T) == typeof(byte)
			 || typeof(T) == typeof(sbyte)
			 || typeof(T) == typeof(short)
			 || typeof(T) == typeof(ushort)
			 || typeof(T) == typeof(int)
			 || typeof(T) == typeof(uint)
			 || typeof(T) == typeof(long)
			 || typeof(T) == typeof(ulong)
			 || typeof(T) == typeof(float)
			 || typeof(T) == typeof(double)
			 || typeof(T) == typeof(decimal)
			 || typeof(T) == typeof(Guid)
			 || typeof(T) == typeof(DateTime)
			 || typeof(T) == typeof(DateTimeOffset)
			 || typeof(T) == typeof(TimeSpan)
			 || typeof(T) == typeof(NodaTime.Instant)
			 || typeof(T) == typeof(NodaTime.Duration)
			 // nullables!
			 || typeof(T) == typeof(bool?)
			 || typeof(T) == typeof(char?)
			 || typeof(T) == typeof(byte?)
			 || typeof(T) == typeof(sbyte?)
			 || typeof(T) == typeof(short?)
			 || typeof(T) == typeof(ushort?)
			 || typeof(T) == typeof(int?)
			 || typeof(T) == typeof(uint?)
			 || typeof(T) == typeof(long?)
			 || typeof(T) == typeof(ulong?)
			 || typeof(T) == typeof(float?)
			 || typeof(T) == typeof(double?)
			 || typeof(T) == typeof(decimal?)
			 || typeof(T) == typeof(Guid?)
			 || typeof(T) == typeof(DateTime?)
			 || typeof(T) == typeof(DateTimeOffset?)
			 || typeof(T) == typeof(TimeSpan?)
			 || typeof(T) == typeof(NodaTime.Instant?)
			 || typeof(T) == typeof(NodaTime.Duration?)
			)
			{
				// version également optimisée!
				foreach (var item in items)
				{
					Add(FromValue<T>(item));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			if (typeof(T) == typeof(JsonValue))
			{
#if NETFRAMEWORK || NETSTANDARD
				// cannot easily re-cast so add manually :(
				foreach (var item in items)
				{
					Add(((JsonValue?) (object?) item) ?? JsonNull.Null);
				}
#else
				var json = MemoryMarshal.CreateReadOnlySpan<JsonValue>(ref Unsafe.As<T, JsonValue>(ref MemoryMarshal.GetReference(items)), items.Length);
				return AddRange(json, deepCopy: false);
#endif
			}

			var dom = CrystalJsonDomWriter.Default;
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(T);
			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, value, type, null));
			}
			return this;
		}

		/// <summary>Append une autre array en copiant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<T>(List<T> items)
		{
			Contract.NotNull(items);

			if (items.Count == 0) return this;

			// pré-alloue si on connaît à l'avance la taille
			EnsureCapacity(this.Count + items.Count);

			#region <JIT_HACK>
			// pattern reconnu et optimisé par le JIT en Release
#if !DEBUG
			if (typeof(T) == typeof(bool)
			 || typeof(T) == typeof(char)
			 || typeof(T) == typeof(byte)
			 || typeof(T) == typeof(sbyte)
			 || typeof(T) == typeof(short)
			 || typeof(T) == typeof(ushort)
			 || typeof(T) == typeof(int)
			 || typeof(T) == typeof(uint)
			 || typeof(T) == typeof(long)
			 || typeof(T) == typeof(ulong)
			 || typeof(T) == typeof(float)
			 || typeof(T) == typeof(double)
			 || typeof(T) == typeof(decimal)
			 || typeof(T) == typeof(Guid)
			 || typeof(T) == typeof(DateTime)
			 || typeof(T) == typeof(DateTimeOffset)
			 || typeof(T) == typeof(TimeSpan)
			 || typeof(T) == typeof(NodaTime.Instant)
			 || typeof(T) == typeof(NodaTime.Duration)
			 // nullables!
			 || typeof(T) == typeof(bool?)
			 || typeof(T) == typeof(char?)
			 || typeof(T) == typeof(byte?)
			 || typeof(T) == typeof(sbyte?)
			 || typeof(T) == typeof(short?)
			 || typeof(T) == typeof(ushort?)
			 || typeof(T) == typeof(int?)
			 || typeof(T) == typeof(uint?)
			 || typeof(T) == typeof(long?)
			 || typeof(T) == typeof(ulong?)
			 || typeof(T) == typeof(float?)
			 || typeof(T) == typeof(double?)
			 || typeof(T) == typeof(decimal?)
			 || typeof(T) == typeof(Guid?)
			 || typeof(T) == typeof(DateTime?)
			 || typeof(T) == typeof(DateTimeOffset?)
			 || typeof(T) == typeof(TimeSpan?)
			 || typeof(T) == typeof(NodaTime.Instant?)
			 || typeof(T) == typeof(NodaTime.Duration?)
			)
			{
				// version également optimisée!
				foreach (var item in items)
				{
					Add(FromValue<T>(item));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			// si c'est déja une liste de JsonValue, on n'a pas besoin de les convertir
			if (items is List<JsonValue> json)
			{
				return AddRange(json, deepCopy: false);
			}

			var dom = CrystalJsonDomWriter.Default;
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(T);
			foreach (var value in items)
			{
				//note: l'overhead de Add() est minimum, donc pas besoin d'optimiser particulièrement ici
				Add(dom.ParseObjectInternal(ref context, value, type, null));
			}
			return this;
		}

		/// <summary>Append une autre array en copiant ou collant ses éléments à la fin</summary>
		/// <param name="array">Array à ajouter</param>
		/// <param name="deepCopy">Si true, clone les éléments de <paramref name="array"/> avant de les copier.</param>
		/// <remarks>Optimisation: utilise Array.Copy() si deepCopy==false</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(JsonArray array, bool deepCopy)
		{
			Contract.NotNull(array);

			int count = array.m_size;
			if (count > 0)
			{
				// resize une seul fois...
				int size = m_size;
				EnsureCapacity(size + count);

				if (deepCopy)
				{ // on doit copier les éléments
					int p = size;
					var myItems = m_items;
					var otherItems = array.m_items;
					for (int i = 0; i < count; i++)
					{
						myItems[p++] = otherItems[i]?.Copy(deep: true) ?? JsonNull.Null;
					}
				}
				else
				{ // on peut transférer directement les éléments
					Array.Copy(array.m_items, 0, m_items, size, count);
				}
				m_size = size + count;
			}
			return this;
		}

		/// <summary>Append une autre array en copiant ou clonant ses éléments à la fin</summary>
		/// <param name="array">Array à ajouter</param>
		/// <param name="deepCopy">Si true, clone les éléments de <paramref name="array"/> avant de les copier.</param>
		/// <remarks>Optimisation: utilise Array.Copy() si deepCopy==false</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(JsonValue[] array, bool deepCopy)
		{
			Contract.NotNull(array);
			return AddRange(array.AsSpan(), deepCopy);
		}

		/// <summary>Append une autre array en copiant ou clonant ses éléments à la fin</summary>
		/// <param name="array">Array à ajouter</param>
		/// <param name="deepCopy">Si true, clone les éléments de <paramref name="array"/> avant de les copier.</param>
		/// <remarks>Optimisation: utilise Array.Copy() si deepCopy==false</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(ReadOnlySpan<JsonValue> array, bool deepCopy)
		{
			if (array.Length > 0)
			{
				// resize une seul fois...
				int size = m_size;
				EnsureCapacity(size + array.Length);

				var items = m_items;
				Contract.Debug.Assert(items != null && size + array.Length <= items.Length);

				if (deepCopy)
				{ // on doit copier les éléments
					int p = size;
					foreach (var item in array)
					{
						items[p++] = item?.Copy(deep: true);
					}
				}
				else
				{ // on peut transférer directement les éléments
					array.CopyTo(items.AsSpan(size)!);
				}
				m_size = size + array.Length;
			}

			return this;
		}

		/// <summary>Append une autre collection en copiant ou clonant ses éléments à la fin</summary>
		/// <param name="collection">Collection à ajouter</param>
		/// <param name="deepCopy">Si true, clone les éléments de <paramref name="collection"/> avant de les copier.</param>
		/// <remarks>Optimisation: utilise collection.CopyTo() si deepCopy==false</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(ICollection<JsonValue> collection, bool deepCopy)
		{
			Contract.NotNull(collection);

			int count = collection.Count;
			if (count > 0)
			{
				// resize une seul fois...
				int size = m_size;
				EnsureCapacity(size + count);

				if (deepCopy)
				{ // need to copy first :(
					var items = m_items;
					int p = size;
					Contract.Debug.Assert(items != null && p + count <= items.Length);
					foreach (var item in collection)
					{
						items[p++] = item.Copy(deep: true);
					}
					Contract.Debug.Assert(p == size + count);
				}
				else
				{ // can copy directory
					collection.CopyTo(m_items!, size);
				}
				m_size = size + count;
			}
			return this;
		}

		/// <summary>Append une séquence d'éléments en copiant ou clonant ses éléments à la fin</summary>
		/// <param name="items">Array à ajouter</param>
		/// <param name="deepCopy">Si true, clone les éléments de <paramref name="items"/> avant de les copier.</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(IEnumerable<JsonValue> items, bool deepCopy)
		{
			Contract.NotNull(items);

			// JsonArray
			if (items is JsonArray jarr)
			{ // optimized
				return AddRange(jarr, deepCopy);
			}

			// Regular Array
			if (items is JsonValue[] arr)
			{ // optimized
				return AddRange(arr, deepCopy);
			}

			// Collection
			if (items is ICollection<JsonValue> c)
			{
				return AddRange(c, deepCopy);
			}

			// Enumerable
			using (var en = items.GetEnumerator())
			{
				while (en.MoveNext())
				{
					Add(deepCopy ? en.Current.Copy(deep: true) : en.Current);
				}
			}
			return this;
		}

		#endregion

		/// <summary>Supprime tous les éléments de cette array</summary>
		/// <remarks>Si le buffer est inférieur à 1024, il est conservé.
		/// Pour supprimer le buffer, il faut appeler <see cref="TrimExcess()"/> juste après <see cref="Clear()"/></remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void Clear()
		{
			int size = m_size;
			if (size > 0 && size <= MAX_KEEP_CAPACITY)
			{ // pas trop grand, on garde le buffer
				Array.Clear(m_items, 0, size);
			}
			else
			{ // clear
				m_items = Array.Empty<JsonValue>();
			}
			m_size = 0;
			//TODO: versionning?
		}

		public bool IsReadOnly => false;

		/// <summary>Retourne la valeur d'un élément d'après son index</summary>
		/// <typeparam name="T">Type attendu de la valeur</typeparam>
		/// <param name="index">Index de l'élément à retourner</param>
		/// <returns>Valeur de l'élément à l'index spécifié, ou une exception si l'index est en dehors des bornes de l'array, où si la valeur ne peut pas être bindé vers le type <typeparamref name="T"/></returns>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> est en dehors des bornes du tableau</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T? Get<T>(int index)
		{
			return (this[index] ?? JsonNull.Missing).As<T>();
		}

		[CollectionAccess(CollectionAccessType.Read)]
		public bool TryGet<T>(int index, [MaybeNullWhen(false)] out T? result)
		{
			if (index >= 0 & index < this.Count)
			{
				result = this[index].As<T>()!;
				return true;
			}
			result = default(T);
			return false;
		}

		/// <summary>Retourne la valeur à l'index spécifié sous forme d'objet JSON</summary>
		/// <param name="index">Index de l'objet à retourner</param>
		/// <returns>Valeur de l'objet à l'index spécifié, ou null si l'array contient null à cet index, ou une exception si l'index est en dehors des bornes de l'array, où si la valeur n'est pas un objet</returns>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> est en dehors des bornes du tableau</exception>
		/// <exception cref="ArgumentException">Si la valeur à l'<paramref name="index"/> spécifié n'est pas un objet JSON.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject? GetObject(int index)
		{
			return this[index]?.AsObject(required: false);
		}

		/// <summary>Retourne la valeur à l'index spécifié sous forme d'objet JSON</summary>
		/// <param name="index">Index de l'objet à retourner</param>
		/// <param name="required">Si true et que l'array contient null à cet index, provoque une exception. Sinon, retourne null.</param>
		/// <returns>Valeur de l'objet à l'index spécifié, ou null si l'array contient null à cet index (et que <paramref name="required"/> est false), ou une exception si l'index est en dehors des bornes de l'array, où si la valeur n'est pas un objet, ou si la valeur est null (et que <paramref name="required"/> est true)</returns>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> est en dehors des bornes du tableau</exception>
		/// <exception cref="InvalidOperationException">Si la valeur à l'<paramref name="index"/> spécifié est null, et que <paramref name="required"/> vaut true.</exception>
		/// <exception cref="ArgumentException">Si la valeur à l'<paramref name="index"/> spécifié n'est pas un objet JSON.</exception>
		[Pure, ContractAnnotation("required:true => notnull"), CollectionAccess(CollectionAccessType.Read)]
		public JsonObject? GetObject(int index, bool required)
		{
			return this[index].AsObject(required);
		}

		/// <summary>Retourne la valeur à l'index spécifié sous forme d'array JSON</summary>
		/// <param name="index">Index de l'array à retourner</param>
		/// <returns>Valeur à l'index spécifié, ou null si l'array contient null à cet index, ou une exception si l'index est en dehors des bornes de l'array, où si la valeur n'est pas une array</returns>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> est en dehors des bornes du tableau</exception>
		/// <exception cref="ArgumentException">Si la valeur à l'<paramref name="index"/> spécifié n'est pas une array JSON.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArray(int index)
		{
			return this[index]?.AsArray(required: false);
		}

		/// <summary>Retourne la valeur à l'index spécifié sous forme d'array JSON</summary>
		/// <param name="index">Index de l'array à retourner</param>
		/// <param name="required">Si true et que l'array contient null à cet index, provoque une exception. Sinon, retourne null.</param>
		/// <returns>Valeur à l'index spécifié, ou null si l'array contient null à cet index (et que <paramref name="required"/> est false), ou une exception si l'index est en dehors des bornes de l'array, où si la valeur n'est pas une array, ou si la valeur est null (et que <paramref name="required"/> est true)</returns>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> est en dehors des bornes du tableau</exception>
		/// <exception cref="InvalidOperationException">Si la valeur à l'<paramref name="index"/> spécifié est null, et que <paramref name="required"/> vaut true.</exception>
		/// <exception cref="ArgumentException">Si la valeur à l'<paramref name="index"/> spécifié n'est pas une array JSON.</exception>
		[Pure, ContractAnnotation("required:true => notnull"), CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArray(int index, bool required)
		{
			return this[index].AsArray(required);
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public int IndexOf(JsonValue item)
		{
			return Array.IndexOf(m_items, item, 0, m_size);
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
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
			if (index < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));

			// s'il n'y a pas assez de place, on doit insérer autant de JsonNull.Null que nécessaire
			var empty = JsonNull.Null;
			if (index >= m_size)
			{
				EnsureCapacity(index + 1);
				var items = m_items;
				for (int i = m_size; i < index; i++)
				{
					items[i] = empty;
				}
				m_size = index + 1;
			}
			m_items[index] = item ?? empty;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Insert(int index, JsonValue? item)
		{
			if ((uint)index > (uint)m_size) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));

			if (m_size == m_items.Length) EnsureCapacity(m_size + 1);
			if (index < m_size)
			{
				Array.Copy(m_items, index, m_items, index + 1, m_size - index);
			}
			m_items[index] = item ?? JsonNull.Null;
			m_size++;
			//TODO: versionning?
		}

		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public bool Remove(JsonValue item)
		{
			int index = IndexOf(item);
			if (index >= 0)
			{
				RemoveAt(index);
				return true;
			}
			return false;
		}

		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveAt(int index)
		{
			if ((uint)index >= (uint)m_size)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException();
			}

			m_size--;
			if (index < m_size)
			{
				Array.Copy(m_items, index + 1, m_items, index, m_size - index);
			}
			m_items[m_size] = null!;

			//TODO: OPTIMIZE: pas forcément une bonne idée pour des algos qui remplissent une array de "TODO" items, et la vident progressivement jusqu'a ce qu'elle soit vide
			// => on pourrait déferrer le shrink en cas d'insert après un delete ?
			// => on ne peut pas non plus shrinker si un insert observe une trop grand capacité, car c'est le cas quand on pré-alloue une array avant de la remplir...
			// => ne rien faire, et rendre le shrink explicite ?
			/* DISABLED
			if (m_size < m_items.Length >> 1)
			{ // shrink??
				this.Capacity = m_items.Length >> 1;
			}
			 */
			//TODO: versionning?
		}

		[CollectionAccess(CollectionAccessType.Read)]
		public void CopyTo(JsonValue[] array)
		{
			CopyTo(array, 0);
		}

		[CollectionAccess(CollectionAccessType.Read)]
		public void CopyTo(JsonValue?[] array, int arrayIndex)
		{
			m_items.AsSpan(0, m_size).CopyTo(array.AsSpan(arrayIndex));
		}

		[CollectionAccess(CollectionAccessType.Read)]
		public void CopyTo(int index, JsonValue?[] array, int arrayIndex, int count)
		{
			if (m_size - index < count) ThrowHelper.ThrowArgumentException(nameof(index), "Offset or length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
			m_items.AsSpan(index, count).CopyTo(array.AsSpan(arrayIndex));
		}

		#endregion

		#region Operators...

		/// <summary>Keep only the elements that match a predicate</summary>
		/// <param name="predicate">Predicate that returns true for elements to keep</param>
		/// <returns>Number of elements that where kept</returns>
		/// <remarks>The original array is modified</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public int KeepOnly([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			// if already empty, nothing much to do
			if (m_size == 0) return 0;

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (predicate(items[i] ?? JsonNull.Null))
				{
					items[p++] = i;
				}
			}
			Contract.Debug.Ensures(p <= m_size);
			m_size = p;
			return p;
		}

		/// <summary>Remove all the elements that match a predicate</summary>
		/// <param name="predicate">Predicate that returns true for elements to remove</param>
		/// <returns>Number of elements that where removed</returns>
		/// <remarks>The original array is modified</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public int RemoveAll([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			// if already empty, nothing much to do
			if (m_size == 0) return 0;

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (!predicate(items[i] ?? JsonNull.Null))
				{
					items[p++] = i;
				}
			}
			Contract.Debug.Assert(p <= m_size);
			int r = m_size;
			for (int i = p; i < r; i++)
			{
				items[i] = null;
			}
			m_size = p;
			return r - p;
		}

		/// <summary>Remove duplicate elements from this array</summary>
		/// <remarks>Similar to Distinct(), except that it modifies the original array.</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveDuplicates()
		{
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
				if (set.Remove(items[i] ?? JsonNull.Null))
				{ // it was in the set, keep it
					items[p++] = items[i];
				}
				//else: it has already been copied
			}
			Contract.Debug.Assert(p == set.Count);
			m_size = p;
		}

		/// <summary>Retourne une nouvelle array ne contenant que les éléments de cette array à partir de l'index indiqué</summary>
		/// <param name="index">Index de l'élément de cette array qui sera le premier dans la nouvelle array</param>
		/// <returns>Nouvelle array (copie de la précédente)</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public JsonArray Substring(int index)
		{
			int count = this.Count - index;

			if (count < 0 || count > this.Count) throw ThrowHelper.ArgumentOutOfRangeIndex(index);
			if (count == 0) return JsonArray.Empty;

			var tmp = new JsonValue[count];
			Array.Copy(m_items, index, tmp, 0, count);
			return new JsonArray(tmp, count);
		}

		[CollectionAccess(CollectionAccessType.Read)]
		public JsonArray Substring(int index, int count)
		{
			//REVIEW: renommer en GetRange? (cf List<T>.GetRange(index, count))

			int remaining = this.Count - index;
			if (remaining < 0 || remaining > this.Count) throw ThrowHelper.ArgumentOutOfRangeIndex(index);

			if (count < 0 || count > remaining) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count));
			if (count == 0) return JsonArray.Empty;

			var tmp = new JsonValue[count];
			Array.Copy(m_items, index, tmp, 0, count);
			return new JsonArray(tmp, count);
		}

		#endregion

		#region Enumerator...

		public Enumerator GetEnumerator()
		{
			return new Enumerator(m_items, m_size);
		}

		IEnumerator<JsonValue> IEnumerable<JsonValue>.GetEnumerator()
		{
			return new Enumerator(m_items, m_size);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public struct Enumerator : IEnumerator<JsonValue>
		{
			private readonly JsonValue?[] m_items;
			private readonly int m_size;
			private int m_index;
			private JsonValue? m_current;

			internal Enumerator(JsonValue?[] items, int size)
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
				//TODO: check versionning?
				if ((uint)m_index < (uint)m_size)
				{
					m_current = m_items[m_index] ?? JsonNull.Null;
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

			object System.Collections.IEnumerator.Current
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

			void System.Collections.IEnumerator.Reset()
			{
				m_index = 0;
				m_current = null;
			}

			public JsonValue Current => m_current!;
		}

		/// <summary>Retourne une vue typée de cette <see cref="JsonArray"/> comme si elle ne contenait que des <see cref="JsonObject"/>s</summary>
		/// <param name="required">Si true, vérifie que chaque élément de l'array n'est pas null</param>
		/// <remarks>
		/// Toute entrée contenant un <see cref="JsonNull"/> retournera <b>null</b>!
		/// Si l'array contient autre chose que des <see cref="JsonObject"/>, une <see cref="InvalidCastException"/> sera générée au runtime lors de l'énumération!
		/// </remarks>
		public JsonArray<JsonObject> AsObjects(bool required = false)
		{
			return new JsonArray<JsonObject>(m_items, m_size, required);
		}

		/// <summary>Retourne une vue typée de cette <see cref="JsonArray"/> comme si elle ne contenait que des <see cref="JsonArray"/>s</summary>
		/// <param name="required">Si true, vérifie que chaque élément de l'array n'est pas null</param>
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
			//note: on ne peut pas appeler cette méthode "As<T>" a cause d'un conflit avec l'extension method As<T> sur les JsonValue!
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
					m_current = default(TValue);
					m_required = required;
				}

				public void Dispose()
				{ }

				public bool MoveNext()
				{
					var arr = m_array;
					//TODO: check versionning?
					if ((uint) m_index < (uint) arr.m_size)
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
					//TODO: check versionning?
					m_index = m_array.m_size + 1;
					m_current = default(TValue);
					return false;
				}

				object System.Collections.IEnumerator.Current
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

				void System.Collections.IEnumerator.Reset()
				{
					//TODO: check versionning?
					m_index = 0;
					m_current = default(TValue);
				}

				public TValue Current => m_current;

			}

		}


		#endregion

		internal override bool IsSmallValue()
		{
			const int LARGE_ARRAY = 5;
			var size = m_size;
			if (size >= LARGE_ARRAY) return false;
			var items = m_items;
			for (int i = 0; i < size; i++)
			{
				if (!(items[i] ?? JsonNull.Null).IsSmallValue()) return false;
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
					return "[ " + (items[0] ?? JsonNull.Null).GetCompactRepresentation(depth + 1) + " ]";
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
			// Dump les 4 premiers, et rajoutes des ", ... X more" si plus que 4.
			// On va quand même dumper le 5 eme s'il y a exactement 5 items, pour éviter le " .... 1 more" qui prend autant de places que de l'écrire!
			++depth;
			sb.Append("[ ").Append((items[0] ?? JsonNull.Null).GetCompactRepresentation(depth));
			if (size >= 2) sb.Append(", ").Append((items[1] ?? JsonNull.Null).GetCompactRepresentation(depth));
			if (size >= 3) sb.Append(", ").Append((items[2] ?? JsonNull.Null).GetCompactRepresentation(depth));
			if (depth == (0+1))
			{ // on va jusqu'à 4 items (5eme joker)
				if (size >= 4) sb.Append(", ").Append((items[3] ?? JsonNull.Null).GetCompactRepresentation(depth));
				if (size == 5) sb.Append(", ").Append((items[4] ?? JsonNull.Null).GetCompactRepresentation(depth));
				else if (size > 5) sb.Append($", /* … {size - 4:N0} more */");
			}
			else
			{ // on va jusqu'à 3 items (4eme joker)
				if (size == 4) sb.Append(", ").Append((items[3] ?? JsonNull.Null).GetCompactRepresentation(depth));
				else if (size > 4) sb.Append($", /* … {size - 3:N0} more */");
			}
			sb.Append(" ]");
			return sb.ToString();
		}

		public override object? ToObject()
		{
			//TODO: détecter le cas ou tt les members ont le même type T,
			// et dans ce cas créer une List<T>, plutôt qu'une List<object> !
			//TODO2: pourquoi ne pas retourner un object[] plutôt qu'une List<object> ?

			var list = new List<object?>(this.Count);
			foreach (var value in this)
			{
				list.Add(value.ToObject());
			}
			return list;
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			return (resolver ?? CrystalJson.DefaultResolver).BindJsonArray(type, this);
		}

		/// <summary>Retourne une <see cref="JsonValue"/>[] contenant les mêmes éléments comme cette array JSON</summary>
		/// <remarks>Effectue une shallow copy des éléments.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonValue[] ToArray()
		{
			if (m_size == 0) return Array.Empty<JsonValue>();
			var res = new JsonValue[m_size];
			Array.Copy(m_items, res, res.Length);
			return res;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T?[] ToArray<T>(ICrystalJsonTypeResolver? resolver = null)
		{
			#region <JIT_HACK>
			// pattern reconnu et optimisé par le JIT en Release
#if !DEBUG
			if (typeof(T) == typeof(bool)
			 || typeof(T) == typeof(char)
			 || typeof(T) == typeof(byte)
			 || typeof(T) == typeof(sbyte)
			 || typeof(T) == typeof(short)
			 || typeof(T) == typeof(ushort)
			 || typeof(T) == typeof(int)
			 || typeof(T) == typeof(uint)
			 || typeof(T) == typeof(long)
			 || typeof(T) == typeof(ulong)
			 || typeof(T) == typeof(float)
			 || typeof(T) == typeof(double)
			 || typeof(T) == typeof(decimal)
			 || typeof(T) == typeof(Guid)
			 || typeof(T) == typeof(DateTime)
			 || typeof(T) == typeof(DateTimeOffset)
			 || typeof(T) == typeof(TimeSpan)
			 || typeof(T) == typeof(NodaTime.Instant)
			 || typeof(T) == typeof(NodaTime.Duration)
			 // nullables!
			 || typeof(T) == typeof(bool?)
			 || typeof(T) == typeof(char?)
			 || typeof(T) == typeof(byte?)
			 || typeof(T) == typeof(sbyte?)
			 || typeof(T) == typeof(short?)
			 || typeof(T) == typeof(ushort?)
			 || typeof(T) == typeof(int?)
			 || typeof(T) == typeof(uint?)
			 || typeof(T) == typeof(long?)
			 || typeof(T) == typeof(ulong?)
			 || typeof(T) == typeof(float?)
			 || typeof(T) == typeof(double?)
			 || typeof(T) == typeof(decimal?)
			 || typeof(T) == typeof(Guid?)
			 || typeof(T) == typeof(DateTime?)
			 || typeof(T) == typeof(DateTimeOffset?)
			 || typeof(T) == typeof(TimeSpan?)
			 || typeof(T) == typeof(NodaTime.Instant?)
			 || typeof(T) == typeof(NodaTime.Duration?)
			)
			{
				// version également optimisée!
				return ToPrimitiveArray<T>();
			}
#endif
			#endregion </JIT_HACK>

			//testé au runtime, mais il est probable que la majorité des array soient des string[] ??
			if (typeof (T) == typeof (string)) return (T[]) (object) ToStringArray();

			var size = m_size;
			if (size == 0) return Array.Empty<T>();
			var result = new T?[size];
			var items = m_items;
			if (resolver == null || resolver == CrystalJson.DefaultResolver)
			{
				for (int i = 0; i < result.Length; i++)
				{
					result[i] = items[i].As<T>();
				}
			}
			else
			{
				for (int i = 0; i < result.Length; i++)
				{
					result[i] = items[i].As<T>(resolver);
				}
			}
			return result;
		}

		[Pure]
		public T[] ToArray<T>([InstantHandle] Func<JsonValue, T> transform)
		{
			Contract.NotNull(transform);
			var size = m_size;
			var items = m_items;
			var arr = new T[size];
			for (int i = 0; i < arr.Length; i++)
			{
				arr[i] = transform(items[i] ?? JsonNull.Null);
			}
			return arr;
		}

		/// <summary>Désérialise une JSON Array en array d'objets dont le type est défini</summary>
		/// <typeparam name="T">Type des éléments de la liste</typeparam>
		/// <param name="value">Tableau JSON contenant des objets a priori de type T</param>
		/// <param name="required"></param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <returns>Retourne une IList&lt;T&gt; contenant les éléments désérialisés</returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static T?[]? BindArray<T>(JsonValue? value, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			if (value == null || value.IsNull) return required ? JsonValueExtensions.FailRequiredValueIsNullOrMissing<T[]>() : null;
			if (!value.IsArray) throw CrystalJson.Errors.Binding_CannotDeserializeJsonTypeIntoArrayOf(value, typeof(T));
			if (!(value is JsonArray array)) throw CrystalJson.Errors.Binding_UnsupportedInternalJsonArrayType(value);
			return array.ToArray<T>(customResolver);
		}

		[Pure, CollectionAccess(CollectionAccessType.Read), UsedImplicitly]
		private T?[] ToPrimitiveArray<T>()
		//IMPORTANT! T doit être un type primitif reconnu par As<T> via compile time scanning!!!
		{
			if (this.Count == 0) return Array.Empty<T?>();
			var result = new T?[this.Count];
			var items = m_items;
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = items[i].As<T>();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool[] ToBoolArray()
		{
			if (this.Count == 0) return Array.Empty<bool>();
			var result = new bool[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToBoolean();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public int[] ToInt32Array()
		{
			if (this.Count == 0) return Array.Empty<int>();
			var result = new int[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToInt32();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public uint[] ToUInt32Array()
		{
			if (this.Count == 0) return Array.Empty<uint>();
			var result = new uint[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToUInt32();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public long[] ToInt64Array()
		{
			if (this.Count == 0) return Array.Empty<long>();
			var result = new long[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToInt64();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ulong[] ToUInt64Array()
		{
			if (this.Count == 0) return Array.Empty<ulong>();
			var result = new ulong[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToUInt64();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public float[] ToSingleArray()
		{
			if (this.Count == 0) return Array.Empty<float>();
			var result = new float[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToSingle();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public double[] ToDoubleArray()
		{
			if (this.Count == 0) return Array.Empty<double>();
			var result = new double[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToDouble();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public decimal[] ToDecimalArray()
		{
			if (this.Count == 0) return Array.Empty<decimal>();
			var result = new decimal[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToDecimal();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Guid[] ToGuidArray()
		{
			if (this.Count == 0) return Array.Empty<Guid>();
			var result = new Guid[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToGuid();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public NodaTime.Instant[] ToInstantArray()
		{
			if (this.Count == 0) return Array.Empty<NodaTime.Instant>();
			var result = new NodaTime.Instant[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToInstant();
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public string?[] ToStringArray()
		{
			if (this.Count == 0) return Array.Empty<string>();

			var result = new string?[this.Count];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = this[i].ToStringOrDefault();
			}
			return result;
		}

		/// <summary>Retourne une <see cref="List&lt;JsonValue&gt;"/> contenant les mêmes éléments comme cette array</summary>
		/// <remarks>Effectue une shallow copy de la liste.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<JsonValue> ToList()
		{
			return new List<JsonValue>(m_items.Take(m_size)!);
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<T?> ToList<T>(ICrystalJsonTypeResolver? resolver = null)
		{
			#region <JIT_HACK>

			// pattern reconnu et optimisé par le JIT en Release
#if !DEBUG
			if (typeof(T) == typeof(bool)
			 || typeof(T) == typeof(char)
			 || typeof(T) == typeof(byte)
			 || typeof(T) == typeof(sbyte)
			 || typeof(T) == typeof(short)
			 || typeof(T) == typeof(ushort)
			 || typeof(T) == typeof(int)
			 || typeof(T) == typeof(uint)
			 || typeof(T) == typeof(long)
			 || typeof(T) == typeof(ulong)
			 || typeof(T) == typeof(float)
			 || typeof(T) == typeof(double)
			 || typeof(T) == typeof(decimal)
			 || typeof(T) == typeof(Guid)
			 || typeof(T) == typeof(DateTime)
			 || typeof(T) == typeof(DateTimeOffset)
			 || typeof(T) == typeof(TimeSpan)
			 || typeof(T) == typeof(NodaTime.Instant)
			 || typeof(T) == typeof(NodaTime.Duration)
			 // nullables!
			 || typeof(T) == typeof(bool?)
			 || typeof(T) == typeof(char?)
			 || typeof(T) == typeof(byte?)
			 || typeof(T) == typeof(sbyte?)
			 || typeof(T) == typeof(short?)
			 || typeof(T) == typeof(ushort?)
			 || typeof(T) == typeof(int?)
			 || typeof(T) == typeof(uint?)
			 || typeof(T) == typeof(long?)
			 || typeof(T) == typeof(ulong?)
			 || typeof(T) == typeof(float?)
			 || typeof(T) == typeof(double?)
			 || typeof(T) == typeof(decimal?)
			 || typeof(T) == typeof(Guid?)
			 || typeof(T) == typeof(DateTime?)
			 || typeof(T) == typeof(DateTimeOffset?)
			 || typeof(T) == typeof(TimeSpan?)
			 || typeof(T) == typeof(NodaTime.Instant?)
			 || typeof(T) == typeof(NodaTime.Duration?)
			)
			{
				// version également optimisée!
				return ToPrimitiveList<T>();
			}
#endif
			#endregion </JIT_HACK>

			var size = m_size;
			var items = m_items;
			var list = new List<T?>(size);
			if (resolver == null || resolver == CrystalJson.DefaultResolver)
			{
				for (int i = 0; i < size; i++)
				{
					list.Add(items[i].As<T>());
				}
			}
			else
			{
				for (int i = 0; i < size; i++)
				{
					list.Add(items[i].As<T>(resolver));
				}
			}
			return list;
		}

		[Pure]
		public List<T> ToList<T>([InstantHandle] Func<JsonValue, T> transform)
		{
			Contract.NotNull(transform);
			var size = m_size;
			var items = m_items;
			var list = new List<T>(size);
			for (int i = 0; i < size; i++)
			{
				list.Add(transform(items[i] ?? JsonNull.Null));
			}
			return list;
		}

		/// <summary>Désérialise une JSON Array en List d'objets dont le type est défini</summary>
		/// <typeparam name="T">Type des éléments de la liste</typeparam>
		/// <param name="value">Tableau JSON contenant des objets a priori de type T</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required"></param>
		/// <returns>Retourne une IList&lt;T&gt; contenant les éléments désérialisés</returns>
		[Pure, ContractAnnotation("value:null => null")]
		public static List<T?>? BindList<T>(JsonValue? value, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			if (value == null || value.IsNull) return required ? JsonValueExtensions.FailRequiredValueIsNullOrMissing<List<T?>>() : null;
			if (!value.IsArray) throw CrystalJson.Errors.Binding_CannotDeserializeJsonTypeIntoArrayOf(value, typeof(T));

			if (!(value is JsonArray array)) throw CrystalJson.Errors.Binding_UnsupportedInternalJsonArrayType(value);

			return array.ToList<T>(customResolver);
		}

		[Pure, CollectionAccess(CollectionAccessType.Read), UsedImplicitly]
		private List<T?> ToPrimitiveList<T>()
			//IMPORTANT! T doit être un type primitif reconnu par As<T> via compile time scanning!!!
		{
			var result = new List<T?>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.As<T>());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<bool> ToBoolList()
		{
			var result = new List<bool>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToBoolean());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<int> ToInt32List()
		{
			var result = new List<int>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToInt32());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public int SumInt32()
		{
			int total = 0;
			foreach (var item in this)
			{
				total += item.ToInt32();
			}
			return total;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<uint> ToUInt32List()
		{
			var result = new List<uint>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToUInt32());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public uint SumUInt32()
		{
			uint total = 0;
			foreach (var item in this)
			{
				total += item.ToUInt32();
			}
			return total;
		}


		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<long> ToInt64List()
		{
			var result = new List<long>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToInt64());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public long SumInt64()
		{
			long total = 0;
			foreach (var item in this)
			{
				total += item.ToInt64();
			}
			return total;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<ulong> ToUInt64List()
		{
			var result = new List<ulong>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToUInt64());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ulong SumUInt64()
		{
			ulong total = 0;
			foreach (var item in this)
			{
				total += item.ToUInt64();
			}
			return total;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<float> ToSingleList()
		{
			var result = new List<float>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToSingle());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public float SumSingle()
		{
			float total = 0;
			foreach (var item in this)
			{
				total += item.ToSingle();
			}
			return total;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<double> ToDoubleList()
		{
			var result = new List<double>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToDouble());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public double SumDouble()
		{
			double total = 0;
			foreach (var item in this)
			{
				total += item.ToDouble();
			}
			return total;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<decimal> ToDecimalList()
		{
			var result = new List<decimal>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToDecimal());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public decimal SumDecimal()
		{
			decimal total = 0;
			foreach (var item in this)
			{
				total += item.ToDecimal();
			}
			return total;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<Guid> ToGuidList()
		{
			var result = new List<Guid>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToGuid());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<NodaTime.Instant> ToInstantList()
		{
			var result = new List<NodaTime.Instant>(this.Count);
			foreach (var item in this)
			{
				result.Add(item.ToInstant());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<string?> ToStringList()
		{
			var result = new List<string?>(this.Count);

			foreach (var item in this)
			{
				result.Add(item.ToStringOrDefault());
			}
			return result;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ImmutableList<T?> ToImmutableList<T>(ICrystalJsonTypeResolver? resolver = null)
		{
			resolver ??= CrystalJson.DefaultResolver;
			var list = ImmutableList.CreateBuilder<T?>();
			Type t = typeof(T);
			foreach (var item in this)
			{
				list.Add((T?) item.Bind(t, resolver)!);
			}
			return list.ToImmutable();
		}

		protected override JsonValue Clone(bool deep)
		{
			return Copy(deep);
		}

		[Pure]
		public new JsonArray Copy()
		{
			return Copy(false);
		}

		[Pure]
		public new JsonArray Copy(bool deep)
		{
			return Copy(this, deep);
		}

		/// <summary>Crée une copie d'une array JSON, en clonant éventuellement ses éléments</summary>
		/// <param name="value">Array JSON à copier</param>
		/// <param name="deep">Si true, clone les éléments de <paramref name="value"/>. Si false, la nouvelle array contiendra les mêmes éléments.</param>
		/// <returns>Clone de <paramref name="value"/>.</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public static JsonArray Copy(JsonArray value, bool deep)
		{
			Contract.NotNull(value);

			var array = new JsonArray(value.Count);
			array.AddRange(value, deep);
			return array;
		}

		/// <summary>Indique si l'array contient au moins un élément</summary>
		/// <returns>True si <see cref="Count"/> &gt; 0; Sinon, false</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool Any()
		{
			return m_size > 0;
		}

		/// <summary>Indique si au moins un élément de l'array est du type indiqué</summary>
		/// <param name="type">Type d'élément JSON recherché</param>
		/// <remarks>Retourne toujours false pour une array vide.</remarks>
		public bool Any(JsonType type)
		{
			int count = m_size;
			var items = m_items;
			for (int i = 0; i < count; i++)
			{
				if ((items[i] ?? JsonNull.Null).Type != type) return true;
			}
			return false;
		}

		/// <summary>Indique si au moins un élément de l'array satisfait le prédicat</summary>
		/// <remarks>Retourne toujours false pour une array vide.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool Any([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			int count = m_size;
			var items = m_items;
			for (int i = 0; i < count; i++)
			{
				if (predicate(items[i] ?? JsonNull.Null)) return true;
			}
			return false;
		}

		/// <summary>Indique si tous les éléments de l'array sont du type indiqué</summary>
		/// <param name="type">Type d'élément JSON recherché</param>
		/// <remarks>Retourne toujours true pour une array vide.</remarks>
		public bool All(JsonType type)
		{
			int count = m_size;
			var items = m_items;
			for (int i = 0; i < count; i++)
			{
				if ((items[i] ?? JsonNull.Null).Type != type) return false;
			}
			return true;
		}

		/// <summary>Indique si tous les éléments de l'array satisfont le prédicat</summary>
		/// <remarks>Retourne toujours true pour une array vide.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool All([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			int count = m_size;
			var items = m_items;
			for (int i = 0; i < count; i++)
			{
				if (!predicate(items[i] ?? JsonNull.Null)) return false;
			}
			return true;
		}

		/// <summary>Détermine si tous les éléments de l'array ont le même type JSON, où s'ils sont de type différent</summary>
		/// <returns>Un <see cref="JsonType"/> si tous les éléments sont du même type, ou null s'il y a au moins deux éléments de type différent</returns>
		/// <remarks>
		/// Ignore les éléments de 'null' de l'array (ie: [ 123, null, 789 ] retournera le type <see cref="JsonType.Number"/>).
		/// Ne peut pas retourner <see cref="JsonType.Null"/>: si l'array ne contient que des 'null', le résultat sera default(<see cref="JsonType"/>).
		/// </remarks>
		public JsonType? GetElementsTypeOrDefault()
		{
			var size = m_size;
			var items = m_items;
			JsonType? type = null;
			for (int i = 0; i < size; i++)
			{
				var t = items[i]?.Type ?? JsonType.Null;
				if (type == null)
				{ // on n'a pas encore eu d'éléments non null
					type = t;
				}
				else if (t != type && t != JsonType.Null)
				{ // compare
					return null;
				}
			}
			return type;
		}

		/// <summary>Copie les champs d'un autre objet dans l'objet courrant</summary>
		/// <param name="other">Autre objet JSON dont les champs vont être copié dans l'objet courant. Tout champ déjà existant sera écrasé.</param>
		/// <param name="deepCopy">Si true, clone tous les champs de <paramref name="other"/> avant de le copier. Sinon, ils sont copiés par référence.</param>
		public void MergeWith(JsonArray other, bool deepCopy = false)
		{
			Merge(this, other, deepCopy);
		}

		public static JsonArray Merge(JsonArray parent, JsonArray other, bool deepCopy = false)
		{
			// we know how to handle:
			// - one or the other is empty
			// - both have same size

			int n = parent.Count;
			if (n == 0) return other.Copy(deepCopy);
			if (other.Count == 0) return parent.Copy(deepCopy);

			if (n == other.Count)
			{
				if (deepCopy) parent = parent.Copy(true);
				for (int i = 0; i < n; i++)
				{
					var left = parent[i];
					var right = other[i];
					switch (left.Type)
					{
						case JsonType.Object:
						{
							parent[i] = JsonObject.Merge((JsonObject) left, right.AsObject(required: true)!, deepCopy);
							break;
						}
						case JsonType.Array:
						{
							parent[i] = Merge((JsonArray) left, right.AsArray(required: true)!, deepCopy);
							break;
						}
						default:
						{
							parent[i] = deepCopy ? right.Copy(true) : right[i];
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
		internal static JsonArray Project(IEnumerable<JsonValue> source, KeyValuePair<string, JsonValue?>[] defaults)
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
						throw ThrowHelper.InvalidOperationException("Cannot project element of type {0} in JSON array", item.Type);
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
			FlattenRecursive(m_items, array, deep ? int.MaxValue : 1);
			return array;
		}

		private static void FlattenRecursive(IEnumerable<JsonValue?> value, JsonArray output, int limit)
		{
			Contract.Debug.Requires(value != null && output != null);
			foreach(var item in value)
			{
				if (limit > 0 && item != null && item.IsArray)
				{
					FlattenRecursive((JsonArray)item, output, limit - 1);
				}
				else
				{
					output.Add(item ?? JsonNull.Null);
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

		private static bool ShouldInlineArray(JsonValue?[] items, int size)
		{
			switch (size)
			{
				case 0: return true;
				case 1: return items[0]?.IsInlinable() ?? true;
				case 2: return (items[0]?.IsInlinable() ?? true) && (items[1]?.IsInlinable() ?? true);
				default: return false;
			}
		}

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			int count = m_size;
			if (count == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			writer.MarkVisited(this);
			var items = m_items;
			bool inline = writer.Indented && ShouldInlineArray(items, m_size);
			if (inline)
			{
				var state = writer.BeginInlineArray();
				writer.WriteInlineHeadSeparator();
				(items[0] ?? JsonNull.Null).JsonSerialize(writer);
				for (int i = 1; i < count; i++)
				{
					writer.WriteInlineTailSeparator();
					(items[i] ?? JsonNull.Null).JsonSerialize(writer);
				}
				writer.EndInlineArray(state);
			}
			else
			{
				var state = writer.BeginArray();
				writer.WriteHeadSeparator();
				(items[0] ?? JsonNull.Null).JsonSerialize(writer);
				for (int i = 1; i < count; i++)
				{
					writer.WriteTailSeparator();
					(items[i] ?? JsonNull.Null).JsonSerialize(writer);
				}
				writer.EndArray(state);
			}
			writer.Leave(this);
		}

		#endregion

		#region IEquatable<...>

		public override bool Equals(JsonValue? value)
		{
			return value?.Type == JsonType.Array && Equals((JsonArray) value);
		}

		public bool Equals(JsonArray? value)
		{
			if (value == null || value.Count != this.Count)
			{
				return false;
			}

			int n = m_size;
			var l = m_items;
			var r = value.m_items;
			for (int i = 0; i < n; i++)
			{
				if (!(l[i] ?? JsonNull.Null).Equals(r[i])) return false;
			}
			return true;
		}

		public override int GetHashCode()
		{
			// le hashcode de l'objet ne doit pas changer meme s'il est modifié (sinon on casse les hashtables!)
			return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

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
				int c = (l[i] ?? JsonNull.Null).CompareTo(r[i]);
				if (c != 0) return c;
			}
			return m_size - other.m_size;
		}

		#endregion

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteByte('[');
			bool first = true;
			foreach (var item in this)
			{
				if (first) first = false; else writer.WriteByte(',');
				item.WriteTo(ref writer);
			}
			writer.WriteByte(']');
		}

	}

	public static class JsonArrayExtensions
	{

		// magic cast entre JsonValue et JsonArray
		// le but est de réduire les faux positifs de nullref avec des outils d'analyse statique de code (R#, ..)

		/// <summary>Vérifie que la valeur n'est pas vide, et qu'il s'agit bien d'une JsonArray.</summary>
		/// <param name="value">Valeur JSON qui doit être une array</param>
		/// <returns>Valeur castée en JsonArray si elle existe. Une exception si la valeur est null, missing, ou n'est pas une array.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> est null, missing, ou n'est pas une array.</exception>
		[Pure, ContractAnnotation("null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use AsArray(required: true) instead", error: true)]
		public static JsonArray AsArray(this JsonValue? value)
		{
			return value.IsNullOrMissing() ? FailArrayIsNullOrMissing()
				: !value.IsArray ? FailValueIsNotAnArray(value) // => throws
				: (JsonArray) value;
		}

		/// <summary>Retourne la valeur JSON sous forme d'array, ou null si elle est null ou manquante.</summary>
		/// <param name="value">Valeur JSON qui doit être soit une array, soit null/missing.</param>
		/// <returns>Valeur castée en JsonArray si elle existe, ou null si la valeur null ou missing. Une exception si la valeur est d'un type différent.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> n'est ni null, ni une array.</exception>
		[Pure, ContractAnnotation("null => null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use AsArray(required: false) instead", error: true)]
		public static JsonArray? AsArrayOrDefault(this JsonValue? value)
		{
			return value.IsNullOrMissing() ? null
				: !value.IsArray ? FailValueIsNotAnArray(value) // => throws
				: (JsonArray) value;
		}

		/// <summary>Vérifie que la valeur n'est pas vide, et qu'il s'agit bien d'une JsonArray.</summary>
		/// <param name="value">Valeur JSON qui doit être une array</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <returns>Valeur castée en JsonArray si elle existe, ou null si la valeur est null/missing et que <paramref name="required"/> vaut false. Throw dans tous les autres cas</returns>
		/// <exception cref="InvalidOperationException">Si <paramref name="value"/> est null ou missing et que <paramref name="required"/> vaut true. Ou si <paramref name="value"/> n'est pas une array.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? AsArray(this JsonValue? value, bool required)
		{
			if (value.IsNullOrMissing())
			{ // null, vide, ...
				// ReSharper disable once ExpressionIsAlwaysNull
				return required ? FailArrayIsNullOrMissing() : null;
			}
			if (value.Type != JsonType.Array)
			{ // non-null mais pas une array
				return FailValueIsNotAnArray(value); // => throws
			}
			return (JsonArray) value;
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonArray FailArrayIsNullOrMissing()
		{
			throw new InvalidOperationException("Required JSON array was null or missing.");
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonArray FailValueIsNotAnArray(JsonValue value)
		{
			throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value.Type);
		}

		#region ToJsonArray...

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this IEnumerable<JsonValue> source)
		{
			//note: même si source est déjà un JsonArray, on veut quand même copier!
			return new JsonArray(source);
		}

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this IEnumerable<JsonArray> source)
		{
			//note: même si source est déja un JsonArray, on veut quand même copier!
			return new JsonArray(source);
		}

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this IEnumerable<JsonObject> source)
		{
			//note: même si source est déja un JsonArray, on veut quand même copier!
			return new JsonArray(source);
		}

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>([InstantHandle] this IEnumerable<TElement> source, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return new JsonArray().AddRange<TElement>(source, settings, resolver);
		}

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>([InstantHandle] this IEnumerable<TElement> source)
		{
			return new JsonArray().AddRange<TElement>(source);
		}

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>(this TElement[] source)
		{
			return new JsonArray().AddRange<TElement>(source);
		}

		/// <summary>Copie les éléments de la séquence source dans une nouvelle JsonArray</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>(this List<TElement> source)
		{
			return new JsonArray().AddRange<TElement>(source);
		}

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArray<TInput>([InstantHandle] this IEnumerable<TInput> source, [InstantHandle] Func<TInput, JsonValue> selector)
		{
			var arr = new JsonArray((source as ICollection<TInput>)?.Count ?? 0);
			foreach (var item in source)
			{
				arr.Add(selector(item));
			}
			return arr;
		}

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArray<TInput>([InstantHandle] this IEnumerable<TInput> source, [InstantHandle] Func<TInput, JsonObject> selector)
		{
			var arr = new JsonArray((source as ICollection<TInput>)?.Count ?? 0);
			foreach (var item in source)
			{
				arr.Add(selector(item));
			}
			return arr;
		}

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArray<TInput>([InstantHandle] this IEnumerable<TInput> source, [InstantHandle] Func<TInput, JsonArray> selector)
		{
			var arr = new JsonArray((source as ICollection<TInput>)?.Count ?? 0);
			foreach (var item in source)
			{
				arr.Add(selector(item));
			}
			return arr;
		}

		/// <summary>Transforme les éléments de la séquence source en une nouvelle JsonArray</summary>
		public static JsonArray ToJsonArray<TInput, TOutput>([InstantHandle] this IEnumerable<TInput> source, [InstantHandle] Func<TInput, TOutput> selector)
		{
			return new JsonArray((source as ICollection<TInput>)?.Count ?? 0).AddRange(source, selector);
		}

		#endregion

		#region Pick...

		/// <summary>Retourne une liste d'objets copiés, et filtrés pour ne contenir que les champs autorisés dans <param name="keys"/>.</summary>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue> source, params string[] keys)
		{
			Contract.NotNull(source);

			return JsonArray.Project(source, JsonObject.CheckProjectionFields(keys, keepMissing: false));
		}

		/// <summary>Retourne une liste d'objets copiés, et filtrés pour ne contenir que les champs autorisés dans <param name="keys"/>.</summary>
		/// <param name="source"></param>
		/// <param name="keys">Liste des clés à conserver sur les éléments de la liste</param>
		/// <param name="keepMissing">Si true, toute propriété manquante sera ajoutée avec la valeur 'null' / JsonNull.Missing.</param>
		/// <returns>Nouvelle liste contenant le résultat de la projection sur chaque élément de la liste</returns>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue> source, IEnumerable<string> keys, bool keepMissing = false)
		{
			Contract.NotNull(source);
			Contract.NotNull(keys);

			return JsonArray.Project(source, JsonObject.CheckProjectionFields(keys, keepMissing));
		}

		/// <summary>Retourne une liste d'objets copiés, et filtrés pour ne contenir que les champs autorisés dans <param name="defaults"/>.</summary>
		/// <param name="source"></param>
		/// <param name="defaults">Liste des clés à conserver sur les éléments de la liste</param>
		/// <returns>Nouvelle liste contenant le résultat de la projection sur chaque élément de la liste</returns>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue> source, IDictionary<string, JsonValue?> defaults)
		{
			Contract.NotNull(source);

			return JsonArray.Project(source, JsonObject.CheckProjectionDefaults(defaults));
		}

		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue> source, object defaults)
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
		/// <param name="customResolver">Résolveur de type custom (ou celui par défaut si null)</param>
		/// <param name="overwrite">Si true, écrase toute valeur existante. Si false, provoque une exception en cas de doublon de clé</param>
		/// <returns>L'annuaire cible passé en paramètre</returns>
		/// <exception cref="System.ArgumentNullException">Si l'un des paramètre requis est null</exception>
		/// <exception cref="System.InvalidOperationException">Si un élément de l'array est null, ou s'il n'est pas dy type attendu</exception>
		/// <remarks>Attention, l'Array ne doit pas contenir de valeur null !</remarks>
		public static IDictionary<TKey, TValue> MapTo<TKey, TValue>(
			[InstantHandle] this IEnumerable<JsonValue> source,
			IDictionary<TKey, TValue> target,
			JsonType? expectedType,
			[InstantHandle] Func<JsonValue, JsonValue> keySelector,
			[InstantHandle] Func<JsonValue, JsonValue> valueSelector,
			ICrystalJsonTypeResolver? customResolver = null,
			bool overwrite = false
		)
		{
			Contract.NotNull(source);
			Contract.NotNull(target);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			customResolver ??= CrystalJson.DefaultResolver;

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

				var key = keySelector(item).As<TKey>(customResolver);
				Contract.Debug.Assert(key != null);
				var value = valueSelector(item).As<TValue>(customResolver)!;

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

		private readonly JsonValue?[] m_items;
		private readonly int m_size;
		private readonly bool m_required;

		internal JsonArray(JsonValue?[] items, int size, bool required)
		{
			m_items = items;
			m_size = size;
			m_required = required;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator()
		{
			return new Enumerator(m_items, m_size, m_required);
		}

		IEnumerator<TJson> IEnumerable<TJson>.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>Enumerator qui cast chaque élément d'une <see cref="JsonArray"/> en un <b>TJson</b>.</summary>
		public struct Enumerator : IEnumerator<TJson>
		{
			private readonly JsonValue?[] m_items;
			private readonly int m_size;
			private int m_index;
			private TJson? m_current;
			private readonly bool m_required;

			internal Enumerator(JsonValue?[] items, int size, bool required)
			{
				m_items = items;
				m_size = size;
				m_index = 0;
				m_current = default(TJson);
				m_required = required;
			}

			public void Dispose()
			{ }

			public bool MoveNext()
			{
				//TODO: check versioning?
				if ((uint) m_index < (uint) m_size)
				{
					if (!(m_items[m_index] is TJson val))
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
				m_current = default(TJson);
				return false;
			}

			object System.Collections.IEnumerator.Current
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

			void System.Collections.IEnumerator.Reset()
			{
				//TODO: check versioning?
				m_index = 0;
				m_current = default(TJson);
			}

			public TJson Current => m_current;
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

		private TJson? GetNextNullOrInvalid(int index)
		{
			var item = m_items[index];
			if (!item.IsNullOrMissing()) throw FailElementAtIndexCannotBeConverted(index, item);
			if (m_required) throw FailElementAtIndexNullOrMissing(index);
			return null;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidCastException FailElementAtIndexCannotBeConverted(int index, JsonValue val) => new InvalidCastException($"The JSON element at index {index} contains a {val.GetType().Name} that cannot be converted into a {typeof(TJson).Name}");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailElementAtIndexNullOrMissing(int index) => new InvalidOperationException($"The JSON element at index {index} is null or missing");

		[Pure]
		public TJson[] ToArray()
		{
			if (m_size == 0) return Array.Empty<TJson>();
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
		public T[] ToArray<T>([InstantHandle] Func<TJson, T> transform)
		{
			Contract.NotNull(transform);

			if (m_size == 0) return Array.Empty<T>();
			var res = new T[m_size];
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
		public List<T> ToList<T>([InstantHandle] Func<TJson, T> transform)
		{
			var res = new List<T>(m_size);
			foreach (var item in this)
			{
				res.Add(transform(item));
			}
			if (m_size != res.Count) throw new InvalidOperationException();
			return res;
		}

	}

}
