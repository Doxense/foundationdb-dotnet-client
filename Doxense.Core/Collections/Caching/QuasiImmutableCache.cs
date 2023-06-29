#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Collections.Caching
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Repr�sente un cache qui ne change pratiquement jamais</summary>
	/// <typeparam name="TKey">Type des cl�s du cache</typeparam>
	/// <typeparam name="TValue">Type des valeurs du cache</typeparam>
	/// <remarks>Cache optimis� pour un scenario ou il y a un nombre restraint de valeurs 'statiques' qui vont �tre cr��es assez rapidement (au d�marrage du processus), puis r�utilis�e en mode lecteur seule, avec �ventuellement quelles ajouts de temps en temps</remarks>
	public sealed class QuasiImmutableCache<TKey, TValue> : ICache<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
		where TKey : notnull
	{

		/// <summary>Version actuelle du cache</summary>
		/// <remarks>Cette instance n'est pas modifi�e! Toute modification au cache est faite dans une copie qui vient remplacer ce champ</remarks>
		private volatile Dictionary<TKey, TValue> m_root;

		/// <summary>Comparateur utilis� pour rechercher une valeur dans le cache</summary>
		private readonly IEqualityComparer<TValue> m_valueComparer;

		/// <summary>Factory optionnelle utilis�e pour cr�er un nouvel item qui ne serait pas pr�sent dans le cache</summary>
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

		/// <summary>Comparateur utilis� pour les cl�s du cache</summary>
		public IEqualityComparer<TKey> KeyComparer => m_root.Comparer;

		/// <summary>Comparateur utilis� pour les valeurs du cache</summary>
		public IEqualityComparer<TValue> ValueComparer => m_valueComparer;

		/// <summary>G�n�rateur (optionnel) utilis� pour g�n�rer des valeurs manquante dans le cache</summary>
		/// <remarks>Uniquement utilis� par <see cref="GetOrAdd(TKey)"/> pour le moment.</remarks>
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
			// il n'y a pas de m�thode sur Dictionary<,> qui accepte un IEqualityComparer<TValue>, donc on est oblig� de scanner nous m�me :(
			// a priori, le foreach sur la collection Values d'un dictionnaire est optimis�, donc ce n'est pas si dramatique...

			if (value == null)
			{ // cas sp�cial pour null
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

		/// <summary>Retourne un entr�e du cache, si elle existe</summary>
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

		/// <summary>Retourne la valeur d'une entr�e dans le cache, en la cr�ant si n�cessaire</summary>
		/// <param name="key">Cl� de l'entr�e recherch�e</param>
		/// <returns>Valeur de l'entr�e si elle existait, ou utilise <see cref="Factory"/> pour g�n�rer la valeur si elle n'existait pas</returns>
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
			// on sait d�j� qu'elle n'existe pas dans le cache
			var factory = m_valueFactory;
			if (factory == null) throw new InvalidOperationException("The cache does not have a default Value factory");
			result = factory(key);
			TryAddInternal(key, result, false, out result);
			return result;
		}

		/// <summary>Retourne la valeur d'une entr�e dans le cache, en la cr�ant si n�cessaire</summary>
		/// <param name="key">Cl� de l'entr�e recherch�e</param>
		/// <param name="value">Valeur qui sera ajout�e dans le cache pour cette cl�, si elle n'existait pas</param>
		/// <returns>Valeur de l'entr�e si elle existait, ou <paramref name="value"/> si elle n'existait pas</returns>
		public TValue GetOrAdd(TKey key, TValue value)
		{
			if (m_root.TryGetValue(key, out var result))
				return result;

			TryAddInternal(key, value, false, out result);
			return result;
		}

		/// <summary>Retourne la valeur d'une entr�e dans le cache, en la cr�ant si n�cessaire</summary>
		/// <param name="key">Cl� de l'entr�e recherch�e</param>
		/// <param name="factory">Lambda qui sera appel�e pour g�n�r�e la valeur � ajouter, si elle n'existait pas</param>
		/// <returns>Valeur de l'entr�e si elle existait, ou le r�sultat de <paramref name="factory"/> si elle n'existait pas</returns>
		/// <reremarks>Attention: certains caches n'offrent aucune garantie sur le fait que valueFactory ne soit pas appel� plusieurs fois!</reremarks>
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

		/// <summary>Retourne la valeur d'une entr�e dans le cache, en la cr�ant si n�cessaire</summary>
		/// <param name="key">Cl� de l'entr�e recherch�e</param>
		/// <param name="factory">Lambda qui sera appel�e pour g�n�r�e la valeur � ajouter, si elle n'existait pas</param>
		/// <param name="state">Valeur pass�e en second param�tre � <param name="factory"/></param>
		/// <returns>Valeur de l'entr�e si elle existait, ou le r�sultat de <paramref name="factory"/> si elle n'existait pas</returns>
		/// <reremarks>Attention: certains caches n'offrent aucune garantie sur le fait que valueFactory ne soit pas appel� plusieurs fois!</reremarks>
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
			// Ce n'est pas grave car on privil�gie justement les perfs pour les HIT (qui n'appellera pas cette m�thode), plut�t que les MISS (qui ne se produit qu'au d�marrage ou peu fr�quemment)

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

		/// <summary>D�fini la valeur d'une entr�e dans le cache</summary>
		/// <param name="key">Cl� dans le cache</param>
		/// <param name="value">Valeur � ins�rer ou modifier</param>
		/// <remarks>Si l'�l�ment existe d�ja, sa valeur est �cras�e par <paramref name="value"/></remarks>
		public void SetItem(TKey key, TValue value)
		{
			TryAddInternal(key, value, true, out TValue _);
		}

		private void TryAddRangeInternal(IEnumerable<KeyValuePair<TKey, TValue>> items, bool allowUpdate)
		{
			Contract.Debug.Requires(items != null);

			// NOTE: les appelant de TryAddInternal ne nous appellent souvent qu'en cas de MISS, du coup on risque de l'appeler deux fois TryGetValue en suivant.
			// Ce n'est pas grave car on privil�gie justement les perfs pour les HIT (qui n'appellera pas cette m�thode), plut�t que les MISS (qui ne se produit qu'au d�marrage ou peu fr�quemment)

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

		/// <summary>Ajout ou modifie une ou plusieurs entr�es dans le cache, en une seule transaction.</summary>
		/// <param name="items">Liste des �l�ments � ajouter ou modifier dans le cache</param>
		/// <remarks>Si un �l�ment existe d�j�, sa valeur est �cras�e par celle dans <paramref name="items"/></remarks>
		public void SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			Contract.NotNull(items);
			TryAddRangeInternal(items, true);
		}

		/// <summary>Ajoute une ou plusieurs nouvelles entr�es dans le cache, en une seule transaction.</summary>
		/// <param name="items">Liste des nouveaux �l�ments qui ne doivent pas exister dans le cache</param>
		/// <remarks>Si un �l�ment existe d�j�, une exception est d�clench�e et aucune modification ne sera apport�e au cache (�quivalent d'un rollback de transaction)</remarks>
		public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			Contract.NotNull(items);
			TryAddRangeInternal(items, false);
		}

		/// <summary>Ajout ou modifie la valeur d'une entr�e dans le cache</summary>
		/// <param name="key">Cl� de l'entr�e</param>
		/// <param name="addValue">Valeur ajout�e, si la cl� n'existait pas</param>
		/// <param name="updateValueFactory">Lambda appel�e avec la valeur pr�c�dente si elle existait d�j�</param>
		/// <returns>True si la valeur a �t� ajout�e, ou false si elle a �t� modifi�e</returns>
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
				{ // Le cache contenait d�j� ce couple key/value, donc ce n'est pas une addition
					return false;
				}

#pragma warning disable 420
				if (object.ReferenceEquals(original, Interlocked.CompareExchange(ref m_root, updated, original)))
#pragma warning restore 420
				{ // on a r�ussi � publier la nouvelle version, on a juste besoin de v�rifier si la cl� existant d�j� dans l'ancienne version
					return flag;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Supprime une entr�e du cache</summary>
		/// <param name="key">Cl� de l'entr�e � supprimer</param>
		/// <returns>True si l'entr�e a �t� supprim�e, false si elle n'existait pas</returns>
		public bool Remove(TKey key)
		{
			var wait = new SpinWait();

			while (true)
			{
				var original = m_root;
				var updated = new Dictionary<TKey, TValue>(original, original.Comparer);
				if (!updated.Remove(key))
				{ // La cl� n'existait d�j� pas
					return false;
				}

#pragma warning disable 420
				if (object.ReferenceEquals(Interlocked.CompareExchange(ref m_root, updated, original), original))
#pragma warning restore 420
				{ // La nouvelle version du cache ne contient plus la cl�
					return true;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Supprime une entr�e du cache, uniquement si elle � une valeur sp�cifique</summary>
		/// <param name="key">Cl� de l'entr�e � supprimer</param>
		/// <param name="expectedValue">Valeur que l'entr�e doit avoir pour �tre supprim�e</param>
		/// <param name="valueComparer">Comparateur optionnel pour les valeurs</param>
		/// <returns>True si l'entr�e existait et avait la valeur attendue, ou false sinon.</returns>
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
				{ // La nouvelle version du cache ne contient plus la cl�
					return true;
				}

				wait.SpinOnce();
			}
		}

		/// <summary>Recherche et supprimer des entr�es du cache</summary>
		/// <exception cref="System.NotSupportedException">Ce type de cache ne permet pas cette op�ration</exception>
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

		/// <summary>Retourne une liste avec une copie de toutes les entr�es du cache</summary>
		public List<KeyValuePair<TKey, TValue>> ToList()
		{
			var list = new List<KeyValuePair<TKey, TValue>>(m_root.Count);
			list.AddRange(m_root);
			return list;
		}

		/// <summary>Retourne une array avec une copie de toutes les entr�es du cache</summary>
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
