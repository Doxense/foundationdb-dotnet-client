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

namespace Doxense.Collections.Caching
{
	using System.Collections;
	using System.Diagnostics.CodeAnalysis;

	/// <summary>Représente un cache qui ne change pratiquement jamais</summary>
	/// <typeparam name="TKey">Type des clés du cache</typeparam>
	/// <typeparam name="TValue">Type des valeurs du cache</typeparam>
	/// <remarks>Cache optimisé pour un scenario ou il y a un nombre restraint de valeurs 'statiques' qui vont être créées assez rapidement (au démarrage du processus), puis réutilisée en mode lecteur seule, avec éventuellement quelles ajouts de temps en temps</remarks>
	public sealed class QuasiImmutableCache<TKey, TValue> : ICache<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
		where TKey : notnull
	{

		/// <summary>Version actuelle du cache</summary>
		/// <remarks>Cette instance n'est pas modifiée! Toute modification au cache est faite dans une copie qui vient remplacer ce champ</remarks>
		private volatile Dictionary<TKey, TValue> m_root;

		/// <summary>Comparateur utilisé pour rechercher une valeur dans le cache</summary>
		private readonly IEqualityComparer<TValue> m_valueComparer;

		/// <summary>Factory optionnelle utilisée pour créer un nouvel item qui ne serait pas présent dans le cache</summary>
		private readonly Func<TKey, TValue>? m_valueFactory;

		public QuasiImmutableCache()
			: this(null, null, null)
		{ }

		public QuasiImmutableCache(Func<TKey, TValue>? valueFactory)
			: this(valueFactory, null, null)
		{ }

		public QuasiImmutableCache(IEqualityComparer<TKey>? keyComparer)
			: this(null, keyComparer, null)
		{ }

		public QuasiImmutableCache(Func<TKey, TValue>? valueFactory, IEqualityComparer<TKey>? keyComparer)
			: this(valueFactory, keyComparer, null)
		{ }

		public QuasiImmutableCache(Func<TKey, TValue>? valueFactory, IEqualityComparer<TKey>? keyComparer, IEqualityComparer<TValue>? valueComparer)
		{
			m_root = new Dictionary<TKey, TValue>(keyComparer ?? EqualityComparer<TKey>.Default);
			m_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			m_valueFactory = valueFactory;
		}

		public QuasiImmutableCache(Dictionary<TKey, TValue> items, bool copy = false, Func<TKey, TValue>? valueFactory = null, IEqualityComparer<TValue>? valueComparer = null)
		{
			if (copy) items = new Dictionary<TKey, TValue>(items, items.Comparer);
			m_root = items;
			m_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			m_valueFactory = valueFactory;
		}

		/// <summary>Gets the number of elements contained in the cache.</summary>
		public int Count => m_root.Count;

		/// <summary>Comparateur utilisé pour les clés du cache</summary>
		public IEqualityComparer<TKey> KeyComparer => m_root.Comparer;

		/// <summary>Comparateur utilisé pour les valeurs du cache</summary>
		public IEqualityComparer<TValue> ValueComparer => m_valueComparer;

		/// <summary>Générateur (optionnel) utilisé pour générer des valeurs manquante dans le cache</summary>
		/// <remarks>Uniquement utilisé par <see cref="GetOrAdd(TKey)"/> pour le moment.</remarks>
		public Func<TKey, TValue>? Factory => m_valueFactory;

		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

		bool ICache<TKey, TValue>.IsCapped => false;

		int ICache<TKey, TValue>.Capacity => int.MaxValue;

		/// <summary>Gets an enumerable collection that contains the keys in the read-only dictionary. </summary>
		public Dictionary<TKey, TValue>.KeyCollection Keys => m_root.Keys;

		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => m_root.Keys;

		/// <summary>Determines whether the read-only dictionary contains an element that has the specified key.</summary>
		public bool ContainsKey(TKey key)
		{
			return m_root.ContainsKey(key);
		}

		/// <summary>Gets an enumerable collection that contains the values in the read-only dictionary.</summary>
		public Dictionary<TKey, TValue>.ValueCollection Values => m_root.Values;

		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => m_root.Values;

		/// <summary>Determines whether the read-only dictionary contains an element that has the specified value.</summary>
		[Pure]
		public bool ContainsValue(TValue? value)
		{
			// il n'y a pas de méthode sur Dictionary<,> qui accepte un IEqualityComparer<TValue>, donc on est obligé de scanner nous même :(
			// a priori, le foreach sur la collection Values d'un dictionnaire est optimisé, donc ce n'est pas si dramatique...

			if (value == null)
			{ // cas spécial pour null
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var item in m_root.Values)
				{
					if (item == null) return true;
				}
			}
			else
			{
				var cmp = m_valueComparer;
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var item in m_root.Values)
				{
					if (cmp.Equals(value, item)) return true;
				}
			}

			return false;
		}

		/// <summary>Retourne un entrée du cache, si elle existe</summary>
		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
		{
			return m_root.TryGetValue(key, out value);
		}

		/// <summary>Gets the element that has the specified key in the read-only dictionary.</summary>
		public TValue this[TKey key]
		{
			[Pure]
			[return:MaybeNull]
			get => m_root.TryGetValue(key, out var result) ? result : default!;
		}

		/// <summary>Retourne la valeur d'une entrée dans le cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <returns>Valeur de l'entrée si elle existait, ou utilise <see cref="Factory"/> pour générer la valeur si elle n'existait pas</returns>
		public TValue GetOrAdd(TKey key)
		{
			if (m_root.TryGetValue(key, out var result))
			{
				return result;
			}
			return GetOrAddSlow(key, out result);
		}

		private TValue GetOrAddSlow(TKey key, out TValue result)
		{
			// on sait déjà qu'elle n'existe pas dans le cache
			var factory = m_valueFactory;
			if (factory == null) throw new InvalidOperationException("The cache does not have a default Value factory");
			result = factory(key);
			TryAddInternal(key, result, false, out result);
			return result;
		}

		/// <summary>Retourne la valeur d'une entrée dans le cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <param name="value">Valeur qui sera ajoutée dans le cache pour cette clé, si elle n'existait pas</param>
		/// <returns>Valeur de l'entrée si elle existait, ou <paramref name="value"/> si elle n'existait pas</returns>
		public TValue GetOrAdd(TKey key, TValue value)
		{
			if (m_root.TryGetValue(key, out var result))
				return result;

			TryAddInternal(key, value, false, out result);
			return result;
		}

		/// <summary>Retourne la valeur d'une entrée dans le cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <param name="factory">Lambda qui sera appelée pour générée la valeur à ajouter, si elle n'existait pas</param>
		/// <returns>Valeur de l'entrée si elle existait, ou le résultat de <paramref name="factory"/> si elle n'existait pas</returns>
		/// <reremarks>Attention: certains caches n'offrent aucune garantie sur le fait que valueFactory ne soit pas appelé plusieurs fois!</reremarks>
		public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
		{
			Contract.Debug.Requires(factory != null);

			if (m_root.TryGetValue(key, out var result))
			{
				return result;
			}

			result = factory(key);
			//REVIEW: si on perd la course, et que result est IDisposable, faut-il appeler IDisposable dessus??
			TryAddInternal(key, result, false, out result);
			return result;
		}

		/// <summary>Retourne la valeur d'une entrée dans le cache, en la créant si nécessaire</summary>
		/// <param name="key">Clé de l'entrée recherchée</param>
		/// <param name="factory">Lambda qui sera appelée pour générée la valeur à ajouter, si elle n'existait pas</param>
		/// <param name="state">Valeur passée en second paramètre à <param name="factory"/></param>
		/// <returns>Valeur de l'entrée si elle existait, ou le résultat de <paramref name="factory"/> si elle n'existait pas</returns>
		/// <reremarks>Attention: certains caches n'offrent aucune garantie sur le fait que valueFactory ne soit pas appelé plusieurs fois!</reremarks>
		public TValue GetOrAdd<TState>(TKey key, Func<TKey, TState, TValue> factory, TState state)
		{
			Contract.Debug.Requires(factory != null);

			if (m_root.TryGetValue(key, out var result))
			{
				return result;
			}

			result = factory(key, state);
			//REVIEW: si on perd la course, et que result est IDisposable, faut-il appeler IDisposable dessus??
			TryAddInternal(key, result, false, out result);
			return result;
		}

		private void TryAddInternal(TKey key, TValue value, bool allowUpdate, out TValue result)
		{
			// NOTE: les appelant de TryAddInternal ne nous appellent souvent qu'en cas de MISS, du coup on risque de l'appeler deux fois TryGetValue en suivant.
			// Ce n'est pas grave car on privilégie justement les perfs pour les HIT (qui n'appellera pas cette méthode), plutôt que les MISS (qui ne se produit qu'au démarrage ou peu fréquemment)

			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
				var updated = original;

				if (!original.TryGetValue(key, out var local))
				{
					updated = new Dictionary<TKey, TValue>(original, original.Comparer)
					{
						{key, value}
					};
					local = value;
				}
				else if (allowUpdate)
				{
					updated = new Dictionary<TKey, TValue>(original, original.Comparer)
					{
						[key] = value
					};
					local = value;
				}

#pragma warning disable 420
				if (object.ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{
					result = local;
					return;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Défini la valeur d'une entrée dans le cache</summary>
		/// <param name="key">Clé dans le cache</param>
		/// <param name="value">Valeur à insérer ou modifier</param>
		/// <remarks>Si l'élément existe déja, sa valeur est écrasée par <paramref name="value"/></remarks>
		public void SetItem(TKey key, TValue value)
		{
			TryAddInternal(key, value, true, out TValue _);
		}

		private void TryAddRangeInternal(IEnumerable<KeyValuePair<TKey, TValue>> items, bool allowUpdate)
		{
			Contract.Debug.Requires(items != null);

			// NOTE: les appelant de TryAddInternal ne nous appellent souvent qu'en cas de MISS, du coup on risque de l'appeler deux fois TryGetValue en suivant.
			// Ce n'est pas grave car on privilégie justement les perfs pour les HIT (qui n'appellera pas cette méthode), plutôt que les MISS (qui ne se produit qu'au démarrage ou peu fréquemment)

			var wait = new SpinWait();

			// vu qu'on peut retry, il faut d'abord accumuler les items dans une liste
			var copy = items.ToList();
			if (copy.Count == 0) return;

			while (true)
			{
				var original = m_root;
				var updated = new Dictionary<TKey, TValue>(original, original.Comparer);
				foreach(var item in copy)
				{
					if (allowUpdate)
					{
						updated[item.Key] = item.Value;
					}
					else
					{
						updated.Add(item.Key, item.Value);
					}
				}

#pragma warning disable 420
				if (object.ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{
					return;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Ajout ou modifie une ou plusieurs entrées dans le cache, en une seule transaction.</summary>
		/// <param name="items">Liste des éléments à ajouter ou modifier dans le cache</param>
		/// <remarks>Si un élément existe déjà, sa valeur est écrasée par celle dans <paramref name="items"/></remarks>
		public void SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			Contract.NotNull(items);
			TryAddRangeInternal(items, true);
		}

		/// <summary>Ajoute une ou plusieurs nouvelles entrées dans le cache, en une seule transaction.</summary>
		/// <param name="items">Liste des nouveaux éléments qui ne doivent pas exister dans le cache</param>
		/// <remarks>Si un élément existe déjà, une exception est déclenchée et aucune modification ne sera apportée au cache (équivalent d'un rollback de transaction)</remarks>
		public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			Contract.NotNull(items);
			TryAddRangeInternal(items, false);
		}

		/// <summary>Ajout ou modifie la valeur d'une entrée dans le cache</summary>
		/// <param name="key">Clé de l'entrée</param>
		/// <param name="addValue">Valeur ajoutée, si la clé n'existait pas</param>
		/// <param name="updateValueFactory">Lambda appelée avec la valeur précédente si elle existait déjà</param>
		/// <returns>True si la valeur a été ajoutée, ou false si elle a été modifiée</returns>
		public bool AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
		{
			var wait = new SpinWait();

			while (true)
			{
				bool flag = false;
				var original = m_root;
				Dictionary<TKey, TValue> updated;

				if (original.TryGetValue(key, out var local))
				{ // update!
					updated = new Dictionary<TKey, TValue>(original, original.Comparer)
					{
						[key] = updateValueFactory(key, local)
					};
				}
				else
				{
					updated = new Dictionary<TKey, TValue>(original, original.Comparer)
					{
						{key, addValue}
					};
					flag = true;
				}

				if (object.ReferenceEquals(updated, original))
				{ // Le cache contenait déjà ce couple key/value, donc ce n'est pas une addition
					return false;
				}

#pragma warning disable 420
				if (object.ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{ // on a réussi à publier la nouvelle version, on a juste besoin de vérifier si la clé existant déjà dans l'ancienne version
					return flag;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Supprime une entrée du cache</summary>
		/// <param name="key">Clé de l'entrée à supprimer</param>
		/// <returns>True si l'entrée a été supprimée, false si elle n'existait pas</returns>
		public bool Remove(TKey key)
		{
			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
				var updated = new Dictionary<TKey, TValue>(original, original.Comparer);
				if (!updated.Remove(key))
				{ // La clé n'existait déjà pas
					return false;
				}

#pragma warning disable 420
				if (object.ReferenceEquals(Interlocked.CompareExchange(ref m_root, updated, original), original))
#pragma warning restore 420
				{ // La nouvelle version du cache ne contient plus la clé
					return true;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Supprime une entrée du cache, uniquement si elle à une valeur spécifique</summary>
		/// <param name="key">Clé de l'entrée à supprimer</param>
		/// <param name="expectedValue">Valeur que l'entrée doit avoir pour être supprimée</param>
		/// <param name="valueComparer">Comparateur optionnel pour les valeurs</param>
		/// <returns>True si l'entrée existait et avait la valeur attendue, ou false sinon.</returns>
		public bool TryRemove(TKey key, TValue expectedValue, IEqualityComparer<TValue>? valueComparer = null)
		{
			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
				var updated = new Dictionary<TKey, TValue>(original, original.Comparer);
				if (!updated.Remove(key))
				{ // n'existe pas
					return false;
				}

				if (valueComparer == null) valueComparer = EqualityComparer<TValue>.Default;
				if (!valueComparer.Equals(original[key], expectedValue))
				{ // pas la bonne valeur !
					return false;
				}

#pragma warning disable 420
				if (object.ReferenceEquals(Interlocked.CompareExchange(ref m_root, updated, original), original))
#pragma warning restore 420
				{ // La nouvelle version du cache ne contient plus la clé
					return true;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Recherche et supprimer des entrées du cache</summary>
		/// <exception cref="System.NotSupportedException">Ce type de cache ne permet pas cette opération</exception>
		int ICache<TKey, TValue>.Cleanup(Func<TKey, TValue, bool> predicate)
		{
			// il faudrait locker pour pouvoir le faire!
			throw new NotSupportedException();
		}

		/// <summary>Removes all items from the collection.</summary>
		public void Clear()
		{
			var empty = new Dictionary<TKey, TValue>(m_root.Comparer);

			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
#pragma warning disable 420
				if (object.ReferenceEquals(Interlocked.CompareExchange(ref m_root, empty, original), original))
#pragma warning restore 420
				{
					return;
				}
				wait.SpinOnce();
			}
		}

		public Dictionary<TKey, TValue>.Enumerator GetEnumerator()
		{
			return m_root.GetEnumerator();
		}

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		{
			return m_root.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			SetItem(item.Key, item.Value);
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		{
			return m_root.TryGetValue(item.Key, out var value) && m_valueComparer.Equals(value, item.Value);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			((ICollection<KeyValuePair<TKey, TValue>>)m_root).CopyTo(array, arrayIndex);
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			return TryRemove(item.Key, item.Value);
		}

		/// <summary>Retourne une liste avec une copie de toutes les entrées du cache</summary>
		public List<KeyValuePair<TKey, TValue>> ToList()
		{
			var list = new List<KeyValuePair<TKey, TValue>>(m_root.Count);
			list.AddRange(m_root);
			return list;
		}

		/// <summary>Retourne une array avec une copie de toutes les entrées du cache</summary>
		public KeyValuePair<TKey, TValue>[] ToArray()
		{
			var root = m_root;
			var array = new KeyValuePair<TKey, TValue>[root.Count];
			int i = 0;
			foreach (var kvp in root)
			{
				array[i] = kvp;
			}
			Contract.Debug.Ensures(i == root.Count);
			return array;
		}

	}

}
