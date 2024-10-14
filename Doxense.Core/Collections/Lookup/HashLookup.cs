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

namespace Doxense.Collections.Lookup
{
	/// <summary>Table de lookup utilisant une HashTable pour le stockage des éléments stockage (clés non ordonnées)</summary>
	/// <typeparam name="TKey">Type des clés</typeparam>
	/// <typeparam name="TElement">Type des éléments</typeparam>
	public class HashLookup<TKey, TElement> : IEnumerable<Grouping<TKey, TElement>> where TKey : notnull
	{

		private readonly Dictionary<TKey, Grouping<TKey, TElement>> m_items;

		public HashLookup()
			: this(EqualityComparer<TKey>.Default)
		{ }

		public HashLookup(IEqualityComparer<TKey> comparer)
			: this(919, comparer) // 919 (0x397) est le nombre premier le plu proche de 1000, ce qui représente une capacité initiale plus que correcte pour un map reduce
		{ }

		public HashLookup(int capacity, IEqualityComparer<TKey> comparer)
		{
			Contract.NotNull(comparer);
			m_items = new Dictionary<TKey, Grouping<TKey, TElement>>(capacity, comparer);
		}

		public HashLookup(HashLookup<TKey, TElement> elements, IEqualityComparer<TKey> comparer)
		{
			Contract.NotNull(elements);
			Contract.NotNull(comparer);
			m_items = new Dictionary<TKey, Grouping<TKey, TElement>>(elements.m_items, comparer);
		}

		public HashLookup(ILookup<TKey, TElement> elements, IEqualityComparer<TKey> comparer)
		{
			Contract.NotNull(elements);
			Contract.NotNull(comparer);

			Dictionary<TKey,Grouping<TKey,TElement>> items;

			if (elements is HashLookup<TKey, TElement> hl)
			{
				//TODO: vérifier si comparer et hl.m_comparer sont compatibles?
				items = new Dictionary<TKey, Grouping<TKey, TElement>>(hl.m_items, comparer);
			}
			else
			{
				items = new Dictionary<TKey, Grouping<TKey, TElement>>(elements.Count, comparer);
				foreach(var grp in elements)
				{
					items.Add(grp.Key, Grouping.Create(grp));
				}
			}
			m_items = items;
		}

		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_items.Count;
		}

		public ICollection<TKey> Keys => m_items.Keys;

		public ICollection<Grouping<TKey, TElement>> Values => m_items.Values;

		public IEqualityComparer<TKey> Comparer => m_items.Comparer;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(TKey key)
		{
			return m_items.ContainsKey(key);
		}

		public IEnumerable<TElement> this[TKey key]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_items[key];
		}

		/// <summary>Retourne le grouping d'une clé d'une hashlookup, ou une valeur par défaut si elle est manquante</summary>
		/// <param name="key">Clé à lire</param>
		/// <param name="missing">Valeur à retourner si la clé est manquante</param>
		/// <returns>Valeur de la clé dans le dictionnaire, ou valeur par défaut si elle est manquante</returns>
		[ContractAnnotation("missing:notnull => notnull")]
		[return: NotNullIfNotNull("missing")]
		public Grouping<TKey, TElement>? GetValueOrDefault(TKey key, Grouping<TKey, TElement>? missing = null)
		{
			var grouping = GetGrouping(key, createIfMissing: false);
			return grouping ?? missing;
		}


		[ContractAnnotation("createIfMissing:true => notnull")]
		public Grouping<TKey, TElement>? GetGrouping(TKey key, bool createIfMissing)
		{
			if (m_items.TryGetValue(key, out var grouping))
			{ // il existait déjà
				return grouping;
			}

			if (!createIfMissing)
			{ // on ne doit pas le créer
				return null;
			}

			// on crée un nouveau grouping (vide, mais avec la place pour un élément)
			grouping = new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = new TElement[1],
				m_count = 0,
			};

			// on ajoute ce grouping au dictionnaire
			m_items[key] = grouping;
			return grouping;
		}

		public Grouping<TKey, TElement> GetOrCreateGrouping(TKey key, out bool created)
		{
			if (m_items.TryGetValue(key, out var grouping))
			{ // il existait déjà
				created = false;
				return grouping;
			}

			// on crée un nouveau grouping (vide, mais avec la place pour un élément)
			grouping = new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = new TElement[1],
				m_count = 0,
			};

			// on ajoute ce grouping au dictionnaire
			m_items[key] = grouping;
			created = true;
			return grouping;
		}

		public Grouping<TKey, TElement> AddOrUpdateGrouping(TKey key, TElement element, out bool created)
		{
			var grp = GetOrCreateGrouping(key, out created);
			grp.Add(element);
			return grp;
		}

		public Grouping<TKey, TElement> AddOrUpdateGrouping(TKey key, IEnumerable<TElement> elements, out bool created)
		{
			if (m_items.TryGetValue(key, out var grouping))
			{ // il existait déjà
				grouping.AddRange(elements);
				created = false;
				return grouping;
			}

			// on crée un nouveau grouping (vide, mais avec la place pour un élément)
			var t = elements.ToArray();
			grouping = new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = t,
				m_count = t.Length,
			};

			// on ajoute ce grouping au dictionnaire
			m_items[key] = grouping;
			created = true;
			return grouping;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(TKey key, TElement element)
		{
			GetGrouping(key, true)!.Add(element);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddRange(TKey key, IEnumerable<TElement> elements)
		{
			GetGrouping(key, true)!.AddRange(elements);
		}

		public bool Remove(TKey key)
		{
			return m_items.Remove(key);
		}

		public bool Remove(TKey key, TElement element, bool cleanupIfEmpty = false)
		{
			var grouping = GetGrouping(key, false);

			bool res = false;
			if (grouping != null)
			{
				res = grouping.Remove(element);
				if (cleanupIfEmpty && grouping.Count == 0)
				{ // remove empty grouping?
					m_items.Remove(key);
				}
			}
			return res;
		}

		public void Clear()
		{
			m_items.Clear();
		}

		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out IEnumerable<TElement> elements)
		{
			elements = GetGrouping(key, false);
			return elements != null;
		}

		/// <summary>Execute, pour chaque grouping qui n'est pas vide, une action</summary>
		/// <param name="handler">Action exécutée sur chaque grouping qui contient des éléments</param>
		/// <returns>Nombre de groupings traités</returns>
		public int ForEach(Action<TKey, TElement[]> handler)
		{
			Contract.NotNull(handler);

			int count = 0;
			foreach(var kvp in m_items.Values)
			{
				if (kvp.m_count == 0) continue;

				if (kvp.m_count == kvp.m_elements.Length)
				{ // si le buffer est à la bonne taille, on peut le passer directement
					handler(kvp.Key, kvp.m_elements);
				}
				else
				{ // sinon on est obligé de le copier
					handler(kvp.Key, kvp.ToArray());
				}
				++count;
			}
			return count;
		}

		/// <summary>Execute, pour chaque grouping qui n'est pas vide, une action</summary>
		/// <param name="handler">Action exécutée sur chaque grouping qui contient des éléments. Le 3ème argument contient le nombre d'éléments dans l'array qui sont utilisables.</param>
		/// <returns>Nombre de groupings traités</returns>
		public int ForEach(Action<TKey, TElement[], int> handler)
		{
			Contract.NotNull(handler);

			int count = 0;
			foreach (var kvp in m_items.Values)
			{
				if (kvp.m_count == 0) continue;

				handler(kvp.Key, kvp.m_elements, kvp.m_count);
				++count;
			}
			return count;
		}

		#region IEnumerator<...>

		public Dictionary<TKey, Grouping<TKey, TElement>>.ValueCollection.Enumerator GetEnumerator()
		{
			return m_items.Values.GetEnumerator();
		}

		IEnumerator<Grouping<TKey, TElement>> IEnumerable<Grouping<TKey, TElement>>.GetEnumerator()
		{
			return m_items.Values.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return m_items.Values.GetEnumerator();
		}

		#endregion

	}

}
