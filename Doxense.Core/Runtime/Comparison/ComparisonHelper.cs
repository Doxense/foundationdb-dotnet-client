#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#nullable enable

namespace Doxense
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	public static class ComparisonHelper
	{

		/// <summary>Helper pour éviter les warning "CompareOfFloatsByEqualityOperator" dans le code</summary>
		/// <remarks>Cette méthode n'a pas vraiment d'intérêt hormis faire taire ponctuellement les analyseurs de code sans devoir forcément désactiver les rules globalement dans le fichier</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Equals(double x, double y)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x == y;
		}

		/// <summary>Helper pour éviter les warning "CompareOfFloatsByEqualityOperator" dans le code</summary>
		/// <remarks>Cette méthode n'a pas vraiment d'intérêt hormis faire taire ponctuellement les analyseurs de code sans devoir forcément désactiver les rules globalement dans le fichier</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Equals(float x, float y)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x == y;
		}

		/// <summary>Helper pour éviter les warning "CompareOfFloatsByEqualityOperator" dans le code</summary>
		/// <remarks>Cette méthode n'a pas vraiment d'intérêt hormis faire taire ponctuellement les analyseurs de code sans devoir forcément désactiver les rules globalement dans le fichier</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsZero(double x)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x == 0.0d;
		}

		/// <summary>Helper pour éviter les warning "CompareOfFloatsByEqualityOperator" dans le code</summary>
		/// <remarks>Cette méthode n'a pas vraiment d'intérêt hormis faire taire ponctuellement les analyseurs de code sans devoir forcément désactiver les rules globalement dans le fichier</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsZero(float x)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x == 0.0f;
		}

		public static bool Equals<T>(T? left, T? right)
			where T : IEquatable<T>
		{
			if (left == null) return right == null;
			if (right == null) return false;
			return left.Equals(right);
		}

		/// <summary>Détermine s'il est absolument certain que deux séquences n'ont pas la même taille (sans avoir besoin de les évaluer)</summary>
		/// <typeparam name="T">Type des éléments</typeparam>
		/// <param name="left">Première séquence</param>
		/// <param name="right">Deuxième séquence</param>
		/// <returns>Retourne true si <paramref name="left"/> est une collection ET si <paramref name="right"/> est aussi une collection ET qu'elle n'ont PAS le même Count. False dans tous les autres cas</returns>
		/// <remarks>Cette méthode ne génère pas de faux positifs, mais peut produire des faux négatifs!
		/// i.e.: elle ne doit être utilisée que pour short-circuiter des comparaisons de séquences quand on peut savoir immédiatement qu'elle n'ont pas la même taille (si elle retourne true).
		/// Dans le cas contraire (elle retourne false), alors il faut quand même évaluer les séquences et les comparer réellement (ie: si ce sont des IEnumerable&lt;&gt; il faudra aller jusqu'au dernier élément de chaque séquence pour le savoir)
		/// </remarks>
		public static bool NotSameCount<T>([NoEnumeration] IEnumerable<T> left, [NoEnumeration] IEnumerable<T> right)
		{
			return left is ICollection<T> leftColl
				&& right is ICollection<T> rightColl
				&& leftColl.Count != rightColl.Count;
		}

		[Pure]
		public static bool Equals<TItem>(TItem[]? left, TItem[]? right)
			where TItem : IEquatable<TItem>
		{
			if (left == null) return right == null;
			if (right == null) return false;
			if (left.Length != right.Length) return false;
			for (int i = 0; i < left.Length; i++)
			{
				if (!left[i].Equals(right[i])) return false;
			}
			return true;
		}

		[Pure]
		public static bool Equals<TItem>(List<TItem>? left, List<TItem>? right)
			where TItem : IEquatable<TItem>
		{
			if (left == null) return right == null;
			if (right == null) return false;
			if (left.Count != right.Count) return false;
			for (int i = 0; i < left.Count; i++)
			{
				if (!left[i].Equals(right[i])) return false;
			}
			return true;
		}

		public static bool Equals<TItem>(IEnumerable<TItem>? left, IEnumerable<TItem>? right)
			where TItem : IEquatable<TItem>
		{
			if (left == null) return right == null;
			if (right == null) return false;
			if (NotSameCount(left, right)) return false;
			using (var it = right.GetEnumerator())
			{
				foreach (var item in left)
				{
					if (!it.MoveNext()) return false;
					if (!item.Equals(it.Current)) return false;
				}
				if (it.MoveNext()) return false;
			}

			return true;
		}

	}

}
