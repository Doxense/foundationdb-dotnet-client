#region Copyright Doxense 2012-2019
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Collections.Lookup
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Tools;
	using JetBrains.Annotations;

	/// <summary>Implémentation d'un grouping de plusieurs éléments sous une même clé</summary>
	/// <typeparam name="TKey">Type des clés</typeparam>
	/// <typeparam name="TElement">Type des éléments</typeparam>
	/// <remarks>C'est une clone fonctionnel de System.Linq.Lookup&lt;K, V&gt;.Grouping&lt;K, V&gt; qui est internal et non modifiable depuis l'extérieur.
	/// A utiliser lorsqu'on a un dictionnaire contenant plusieurs valeurs pour chaque clé. Peut également servir de node dans une liste chainée, ou éventuellement un B-Tree</remarks>
	public class Grouping<TKey, TElement> : IGrouping<TKey, TElement>, IList<TElement>
	{
		// IMPORTANT: contrairement à l'implémentation dans LINQ (qui est readonly), le contenu est MODIFIABLE !
		// => il est possible d'ajouter/retirer/filtrer des valeurs de manière dynamique

		// Autre détail important : l'implémentation de LINQ est faite pour fonctionner avec des Lookup<K, V> qui a sa propre implémentation de Hashtable en utilisant les Grouping<K, V> comme "buckets" (chainage)
		// => notre implémentation sera plutôt utilisée dans un Dictionary<K, V> ou SortedSet<T> par exemple (RB-Trees).

		#region Private Members...

		// note: ces valeurs sont modifiées directement par le gars qui va nous créer

		/// <summary>Clé du grouping</summary>
		internal TKey m_key;

		/// <summary>Tableau contenant les éléments</summary>
		internal TElement[] m_elements;

		/// <summary>Nombre d'éléments dans le tableau</summary>
		internal int m_count;

		#endregion

		#region Public Properties...

		/// <summary>Gets the key of the Doxense.Collections.Lookup.Grouping&lt;TKey, TElement&gt;</summary>
		public TKey Key
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_key;
		}

		/// <summary>Gets the number of elements contained in the Doxense.Collections.Lookup.Grouping&lt;TKey, TElement&gt;</summary>
		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_count;
		}

		/// <summary>Retourne le premier élément du grouping (ou default(T) si vide)</summary>
		public TElement? Head
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_count > 0 ? m_elements[0] : default;
		}

		/// <summary>Retourne le dernier élément du grouping (ou default(T) si vide)</summary>
		public TElement? Last
		{
			get => m_count > 0 ? m_elements[m_count - 1] : default;
		}
		//REVIEW: soit "Head/Tail" ou "First"/"Last"

		public TElement this[int index]
		{
			get
			{
				if (index < 0 || index >= m_count) ThrowHelper.ThrowArgumentOutOfRangeIndex(index);
				return m_elements[index];
			}
			[DoesNotReturn]
			set => throw new NotSupportedException();
		}

		#endregion

		#region Public Methods...

		/// <summary>Ajoute un élément dans le grouping</summary>
		/// <param name="element"></param>
		public void Add(TElement element)
		{
			int count = m_count;
			if (count == m_elements.Length)
			{
				// note: une Array ne peux pas avoir plus de 2^31 (~2 milliards) d'éléments, car 2^32 > int.MaxValue
				Array.Resize(ref m_elements, checked(count * 2));
				// note: il y a une autre limitation, qui est qu'un objet .NET ne peut pas dépasser 2 Go en mémoire, cad 2^29 éléments pour un tableau d'objets. (cf http://blogs.msdn.com/b/joshwil/archive/2005/08/10/450202.aspx)
				// le comportement au runtime est inconnu, mais je suppose qu'il va y avoir une OutOfMemoryException ?
				//NOTE: il est possible d'activer le support d'arrays > 2 Go depuis .NET 4.5 sur 64 bits via gcAllowVeryLargeObjects, mais garde la limite de 2^31 éléments : http://msdn.microsoft.com/en-US/library/hh285054(v=vs.110).aspx
			}
			m_elements[count] = element;
			m_count = count + 1;
		}

		private void EnsureCapacity(int capacity)
		{
			if (m_elements.Length < capacity)
			{ // trop court, il va falloir resizer
				capacity = BitHelpers.NextPowerOfTwo(capacity);
				Array.Resize(ref m_elements, capacity);
			}
		}

		/// <summary>Ajoute une liste d'éléments dans le grouping</summary>
		/// <param name="elements">Séquence de nouveaux éléments (peut être vide)</param>
		public void AddRange(IEnumerable<TElement> elements)
		{
			if (elements is ICollection<TElement> collection)
			{ // on connaît le nombre, donc on va pouvoir resizer et copier en une fois

				int n = collection.Count;
				if (n == 0) return;

				// vérifie si on a la capacité nécessaire pour accueillir les éléments
				EnsureCapacity(m_count + n);
				collection.CopyTo(m_elements, m_count);
				m_count += n;
			}
			else
			{ // on ne connaît pas le nombre, donc il va falloir les ajouter un par un :(
				foreach (var element in elements)
				{
					Add(element);
				}
			}
		}

		/// <summary>Concatène un grouping à la fin du grouping courant</summary>
		/// <param name="grouping">Grouping à ajouter en fin de l'instance courante</param>
		public void AddRange(Grouping<TKey, TElement> grouping)
		{
			Contract.NotNull(grouping);

			int n = grouping.m_count;
			if (n == 0) return;

			// ajoutes les éléments à la fin de notre buffer, en se resizant si besoin
			EnsureCapacity(m_count + n);
			Array.Copy(grouping.m_elements, 0, m_elements, m_count, n);
			m_count += n;
		}

		/// <summary>Supprime un élément du grouping</summary>
		/// <param name="element">Element à supprimer</param>
		/// <returns>Retourne true si l'élément était présent et a été supprimé, sinon retourne false</returns>
		/// <remarks>Utilise EqualityComparer&lt;T&gt;.Default pour comparer les éléments.
		/// ATTENTION: Ne supprime que la première occurence trouvée de l'élément !</remarks>
		public bool Remove(TElement element)
		{
			if (m_count >= 0)
			{
				int index = Array.IndexOf<TElement>(m_elements, element, 0, m_count);
				if (index >= 0)
				{
					RemoveAtInternal(index);
					return true;
				}
			}
			return false;
		}

		/// <summary>Supprime un élément du grouping, en utilisant un comparer spécifique</summary>
		/// <param name="element">Element à supprimer</param>
		/// <param name="comparer">EqualityComparer utilisé pour retrouver l'élément</param>
		/// <returns>Retourne true si l'élément était présent et a été supprimé, sinon retourne false</returns>
		/// <remarks>ATTENTION: Ne supprime que la première occurence trouvée de l'élément !</remarks>
		public bool Remove(TElement element, IEqualityComparer<TElement> comparer)
		{
			Contract.NotNull(comparer);

			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (comparer.Equals(element, elements[i]))
				{
					RemoveAtInternal(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>Supprime un élément en fonction de sa position</summary>
		/// <param name="index"></param>
		public void RemoveAt(int index)
		{
			if ((uint) index >= m_count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(index));
			RemoveAtInternal(index);
		}

		/// <summary>Retourne le dernier élément en le retirant du grouping</summary>
		/// <returns>Dernier élément (qui est supprimé)</returns>
		/// <exception cref="System.InvalidOperationException">Si le grouping était vide</exception>
		public TElement Pop()
		{
			if (m_count == 0) throw new InvalidOperationException("Cannot remove last item from an empty grouping");
			int p = m_count - 1;
			var result = m_elements[p];
			RemoveAtInternal(p);
			return result;
		}

		/// <summary>Update le dernier élément, ou crée-le si le grouping était vide</summary>
		/// <param name="newValue">Nouvelle valeur du dernier élément</param>
		public void UpdateLast(TElement newValue)
		{
			if (m_count == 0)
				Add(newValue);
			else
				m_elements[m_count - 1] = newValue;
		}

		/// <summary>Vide le contenu du grouping</summary>
		public void Clear()
		{
			if (m_count > 0)
			{
				if (m_elements.Length <= 4)
				{ // on peut réutiliser le tableau (en le vidant)
					for (int i = 0; i < m_count; i++)
					{
						m_elements[i] = default!;
					}
				}
				else
				{ // on repart de zéro
					m_elements = new TElement[1];
				}
				m_count = 0;
			}
		}

		/// <summary>Supprime tout les éléments matchant une condition particulière</summary>
		/// <param name="match">Fonction qui retourne true pour les éléments à supprimer</param>
		/// <remarks>Nombre d'éléments qui ont été supprimés</remarks>
		public int RemoveAll(Func<TElement, bool> match)
		{
			Contract.NotNull(match);

			//note: Implémentation copiée/collée de List<T>.RemoveAll(...) du .NET 4.5.... So sue me !
			// => l'avantage de cet algo, c'est qu'il effectue le filtrage en une seule passe

			// d'abord on recherche le premier match
			// => tout le début du tableau sera conservé tel quel
			int count = m_count;
			var elements = m_elements;
			int index = 0;
			while ((index < count) && !match(elements[index]))
			{ // ces éléments doivent être conservés
				index++;
			}

			if (index >= count)
			{ // aucun élément à supprimer => bye bye
				return 0;
			}

			// on va avancer dans le tableau avec 'cursor' qui est la position 'read', et 'index' qui est la position 'write'
			// a chaque fois qu'on a un match sur T[cursor], on avance 'cursor'. Sinon, on copie l'élément T[cursor] vers T[index] et on avance les deux positions.
			int cursor = index + 1;
			while (cursor < count)
			{
				// tant que c'est un match, on avance le curseur
				while ((cursor < count) && match(elements[cursor]))
				{
					cursor++;
				}
				// soit on est a la fin, soit on a un élément a conserver
				if (cursor < count)
				{ // on le copie
					elements[index++] = elements[cursor++];
				}
			}
			// pour éviter les leaks, on va vider la fin du buffer
			Array.Clear(elements, index, count - index);
			int deleted = count - index;
			m_count = index;
			ShrinkIfNeeded();
			return deleted;
		}

		/// <summary>Suppression d'un élément en fonction de sa position</summary>
		/// <param name="index"></param>
		private void RemoveAtInternal(int index)
		{
			var elements = m_elements;
			int after = m_count - index - 1;
			if (after > 0)
			{ // si ce n'est pas le dernier, il faut shifter  les items suivants
				Array.Copy(elements, index + 1, elements, index, after);
			}
			--m_count;

			// vide le dernier slot pour éviter les leaks
			elements[m_count] = default!;

			ShrinkIfNeeded();
		}

		/// <summary>Essayes de réduire la taille du buffer si c'est possible</summary>
		/// <remarks>Divise par 2 la taille du buffer si moins d'1/8eme est occupé</remarks>
		private void ShrinkIfNeeded()
		{
			// si on redescend a moins de 1/8eme d'alloué, on shrink le buffer par 50%
			int n = m_elements.Length;
			if (m_count < (n >> 3) && n > 1)
			{
				Array.Resize(ref m_elements, n >> 1);
			}
		}

		public int IndexOf(Func<TElement, bool> match)
		{
			int n = m_count;
			var elements = m_elements;
			for (int i = 0; i < n; i++)
			{
				if (match(elements[i])) return i;
			}
			return -1;
		}

		public ReadOnlySpan<TElement> Span
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_elements.AsSpan(0, m_count);
		}

		public TElement[] ToArray()
		{
			int count = m_count;
			var elements = m_elements;
			if (count == 0) return Array.Empty<TElement>();

			var t = new TElement[count];
			if (count == 1)
			{
				t[0] = elements[0];
			}
			else
			{
				Array.Copy(elements, 0, t, 0, count);
			}
			return t;
		}

		public List<TElement> ToList()
		{
			int count = m_count;
			var elements = m_elements;

			var list = new List<TElement>(count);
			for (int i = 0; i < count; i++)
			{
				list.Add(elements[i]);
			}
			return list;
		}

		/// <summary>Effectue une action sur chaque élément du grouping</summary>
		/// <param name="action">Action prenant un élément</param>
		public void Visit(Action<TElement> action)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				action(elements[i]);
			}
		}

		/// <summary>Effectue une action sur chaque élément du grouping</summary>
		/// <param name="action">Action prenant un élément</param>
		public void Visit(Action<TKey, TElement> action)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				action(m_key, elements[i]);
			}
		}

		/// <summary>Transforme le grouping en une séquence de Key/Value</summary>
		/// <param name="selector">Filtre optionnel (seul les éléments passant se filtre sont retournés</param>
		/// <returns>Séquence de KeyValuePair où la Key celle du grouping, et la Value est un élément du grouping</returns>
		public IEnumerable<KeyValuePair<TKey, TElement>> Map(Func<TElement, bool>? selector = null)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (selector == null || selector(elements[i]))
					yield return new KeyValuePair<TKey, TElement>(m_key, elements[i]);
			}
		}

		#endregion

		#region IEnumerable<TElement> ...

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct Enumerator : IEnumerator<TElement>
		{
			// Inspiré de List<T>.Enumerator et Dictionary<T>.Enumerator:
			// L'enumerator est un struct, et Grouping<> l'expose directement via le GetEnumerator() public
			// => si l'appelant fait "foreach(var x in grp)" ou grp est de type Grouping<>, alors le compilateur allouera le struct dans la stack
			// => si l'appelant passe par IEnumerable<Grouping<>> il aura une version boxée en heap (donc une allocation)

			private readonly Grouping<TKey, TElement> m_grouping;
			private int m_index;
			private TElement m_current;

			internal Enumerator(Grouping<TKey, TElement> grouping)
			{
				m_grouping = grouping;
				m_index = 0;
				m_current = default!;
			}

			public bool MoveNext()
			{
				var grouping = m_grouping;
				if (m_index < grouping.m_count)
				{
					m_current = grouping.m_elements[m_index];
					++m_index;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				m_index = m_grouping.m_count + 1;
				m_current = default!;
				return false;
			}

			public TElement Current
			{
				[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
				[return: MaybeNull]
				get => m_current;
			}

			object? System.Collections.IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index == (m_grouping.Count + 1))
					{
						ThrowEnumOpCantHappen();
					}
					return m_current;
				}
			}

			[DoesNotReturn, ContractAnnotation("=> halt")]
			private static void ThrowEnumOpCantHappen()
			{
				throw new InvalidOperationException("Enumeration has either not started or has already finished.");
			}

			void System.Collections.IEnumerator.Reset()
			{
				m_index = 0;
				m_current = default!;
			}

			[System.Runtime.TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
			public void Dispose()
			{
				//NOP
			}
		}

		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		public Enumerator GetEnumerator()
		{
			return new Enumerator(this);
		}

		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
		{
			return new Enumerator(this);
		}

		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		#endregion

		#region ICollection<TElement> ...

		bool ICollection<TElement>.Contains(TElement item)
		{
			for (int i = 0; i < m_count; i++)
			{
				if (object.Equals(item, m_elements[i])) return true;
			}
			return false;
		}

		/// <summary>Copie le contenu de ce grouping dans un tableau</summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		/// <remarks>Le tableau de destination doit être assez grand pour recevoir tous les éléments!</remarks>
		/// <exception cref="System.ArgumentNullException">La paramètre 'array' est null</exception>
		/// <exception cref="System.InvalidOperationException">Le tableau de destination n'est pas assez grand, ou l'offset est inférieur à 0</exception>
		public void CopyTo(TElement[] array, int arrayIndex)
		{
			// On sous-traite le boulot :)
			Array.Copy(m_elements, 0, array, arrayIndex, m_count);
		}

		bool ICollection<TElement>.IsReadOnly => false;

		#endregion

		#region IList<TElement> ...

		int IList<TElement>.IndexOf(TElement item)
		{
			return Array.IndexOf(m_elements, item, 0, m_count);
		}

		void IList<TElement>.Insert(int index, TElement item)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region Pseudo-LINQ

		public bool Any(Func<TElement, bool> predicate)
		{
			int count = m_count;
			if (count == 0) return false;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (predicate(elements[i])) return true;
			}
			return false;
		}

		public bool All(Func<TElement, bool> predicate)
		{
			int count = m_count;
			if (count == 0) return false;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (!predicate(elements[i])) return false;
			}
			return true;
		}

		public IEnumerable<TElement> Where(Func<TElement, bool> predicate)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				var element = elements[i];
				if (predicate(element)) yield return element;
			}
		}

		#endregion

		public sealed class EqualityComparer : IEqualityComparer<Grouping<TKey, TElement>>, IEqualityComparer<TKey>
		{
			private readonly IEqualityComparer<TKey> m_comparer;

			public EqualityComparer()
			{
				m_comparer = EqualityComparer<TKey>.Default;
			}

			public EqualityComparer(IEqualityComparer<TKey> comparer)
			{
				Contract.NotNull(comparer);
				m_comparer = comparer;
			}

			public bool Equals(TKey? x, TKey? y)
			{
				return m_comparer.Equals(x, y);
			}

			public int GetHashCode(TKey obj)
			{
				return m_comparer.GetHashCode(obj);
			}

			public bool Equals(Grouping<TKey, TElement>? x, Grouping<TKey, TElement>? y)
			{
				return x == null ? y == null : y != null && m_comparer.Equals(x.Key, y.Key);
			}

			public int GetHashCode(Grouping<TKey, TElement> obj)
			{
				return obj == null ? -1 : m_comparer.GetHashCode(obj.Key);
			}
		}

		public sealed class Comparer : IComparer<Grouping<TKey, TElement>>, IComparer<TKey>
		{
			public static readonly IComparer<Grouping<TKey, TElement>> Default = new Comparer();

			private readonly IComparer<TKey> m_comparer;

			public Comparer()
			{
				m_comparer = Comparer<TKey>.Default;
			}

			public Comparer(IComparer<TKey> comparer)
			{
				Contract.NotNull(comparer);
				m_comparer = comparer;
			}

			public int Compare(TKey x, TKey y)
			{
				return m_comparer.Compare(x, y);
			}

			public int Compare(Grouping<TKey, TElement> x, Grouping<TKey, TElement> y)
			{
				return x == null ? (y == null ? 0 : -1)
					: y == null ? +1
					: m_comparer.Compare(x.Key, y.Key);
			}
		}

	}

	public static class Grouping
	{

		/// <summary>Crée un nouveau grouping contenant un élément</summary>
		/// <param name="key">Clé du nouveau grouping</param>
		/// <param name="element">Element stocké dans le grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, TElement element)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = new TElement[1] { element },
				m_count = 1
			};
		}

		/// <summary>Crée un nouveau grouping contenant une liste d'éléments</summary>
		/// <param name="key">Clé du nouveau grouping</param>
		/// <param name="elements">Liste des éléments stockés dans le grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, params TElement[] elements)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = elements,
				m_count = elements.Length
			};
		}

		/// <summary>Crée un nouveau grouping contenant une liste d'éléments</summary>
		/// <param name="key">Clé du nouveau grouping</param>
		/// <param name="elements">Liste des éléments stockés dans le grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, ICollection<TElement> elements)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = elements.ToArray(),
				m_count = elements.Count
			};
		}

		/// <summary>Crée un nouveau grouping contenant une liste d'éléments</summary>
		/// <param name="key">Clé du nouveau grouping</param>
		/// <param name="elements">Liste des éléments stockés dans le grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
		{
			var t = elements.ToArray();
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = t,
				m_count = t.Length
			};
		}

		/// <summary>Convertit un IGrouping LINQ</summary>
		/// <param name="grouping">Grouping LINQ à convertir en Grouping modifiable</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(IGrouping<TKey, TElement> grouping)
		{
			var t = grouping.ToArray();
			return new Grouping<TKey, TElement>
			{
				m_key = grouping.Key,
				m_elements = t,
				m_count = t.Length
			};
		}

		/// <summary>Crée un nouveau grouping à partir d'un KeyValuePair</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <typeparam name="TElement"></typeparam>
		/// <param name="pair">Pair de clé/valeur</param>
		/// <returns></returns>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, TElement> pair)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = pair.Key,
				m_elements = new TElement[1] { pair.Value },
				m_count = 1
			};
		}

		/// <summary>Crée un nouveau grouping à partir d'un KeyValuePair</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <typeparam name="TElement"></typeparam>
		/// <param name="pair">Pair de clé/valeurs</param>
		/// <returns></returns>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, TElement[]> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Crée un nouveau grouping à partir d'un KeyValuePair</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <typeparam name="TElement"></typeparam>
		/// <param name="pair">Pair de clé/valeurs</param>
		/// <returns></returns>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, ICollection<TElement>> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Crée un nouveau grouping à partir d'un KeyValuePair</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <typeparam name="TElement"></typeparam>
		/// <param name="pair">Pair de clé/valeurs</param>
		/// <returns></returns>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, IEnumerable<TElement>> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Convertit un grouping LINQ</summary>
		/// <param name="grouping"></param>
		/// <returns></returns>
		[ContractAnnotation("null => null; notnull => notnull")]
		public static Grouping<TKey, TElement>? FromLinq<TKey, TElement>(IGrouping<TKey, TElement> grouping)
		{
			if (grouping == null) return null;
			// C'est peut être déjà dans le bon type ?
			return (grouping as Grouping<TKey, TElement>) ?? Create(grouping);
		}
	}

}
