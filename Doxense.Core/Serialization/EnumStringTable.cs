#region Copyright Doxense 2014-2020
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Helper pour la mise en cache de conversion d'�num�ration en string</summary>
	[Obsolete("Performance of enum ToString() has been fixed in .NET Core 3.0. This type may not be necessary anymore!")]
	public static class EnumStringTable
	{
		//NOTE: cette classe existe pour palier a un probl�me de performance du Enum.ToString() dans les versions classique du .NET Framework
		// Les derni�res version de .NET Core ont fortement optimis� ce cas, ce qui rend cette classe beaucoup moins indispensable
		// REVIEW: �ventuellement la supprimer, si les benchs montrent qu'il n'y a plus de raison de l'utiliser?

		private static Dictionary<Type, EnumStringTable.Cache> Types = new Dictionary<Type, EnumStringTable.Cache>(TypeEqualityComparer.Default);

		public class Entry
		{
			public readonly Enum Value;
			public readonly string Literal;
			public readonly string Name;
			public readonly string CamelCased;

			internal Entry(Enum value, string literal, string name)
			{
				this.Value = value;
				this.Literal = literal;
				this.Name = name;
				this.CamelCased = EnumStringTable.CamelCase(name);
			}
		}

		/// <summary>Cache contenant les string d'un type d'�num�ration sp�cifique</summary>
		[DebuggerDisplay("Type={m_enumType.Name}")]
		public sealed class Cache
		{

			private readonly Type m_enumType;
			private readonly Dictionary<Enum, Entry> m_cache;

			internal Cache(Type enumType, Dictionary<Enum, Entry> cache)
			{
				Contract.Debug.Requires(enumType != null && cache != null);
				m_enumType = enumType;
				m_cache = cache;
			}

			public Type EnumType => m_enumType;

			public string? GetLiteral(Enum value)
			{
				if (value == null) return null;
				if (m_cache.TryGetValue(value, out EnumStringTable.Entry name))
				{
					return name.Literal;
				}
				else
				{
					return value.ToString("D");
				}
			}

			public string? GetName(Enum value)
			{
				if (value == null) return null;
				if (m_cache.TryGetValue(value, out EnumStringTable.Entry name))
				{
					return name.Name;
				}
				else
				{
					return value.ToString("G");
				}
			}

			public string? GetNameCamelCased(Enum value)
			{
				if (value == null) return null;
				if (m_cache.TryGetValue(value, out EnumStringTable.Entry name))
				{
					return name.CamelCased;
				}
				else
				{
					return value.ToString("G");
				}
			}

		}

		/// <summary>Retourne le cache correspondant � une �num�ration sp�cifique</summary>
		public static EnumStringTable.Cache GetCacheForType(Type enumType)
		{
			var types = EnumStringTable.Types;
			if (!types.TryGetValue(enumType, out Cache cache))
			{
				cache = AddEnumToCache(enumType);
			}
			return cache;
		}

		/// <summary>Retourne le cache correspondant � une �num�ration sp�cifique</summary>
		public static EnumStringTable.Cache GetCacheForType<TEnum>()
			where TEnum : struct, Enum
		{
			return GetCacheForType(typeof(TEnum));
		}

		/// <summary>Retourne le nom correspond � la valeur d'une �num�ration</summary>
		[return: NotNullIfNotNull("value")] 
		public static string? GetName(Type enumType, Enum value)
		{
			return GetCacheForType(enumType).GetName(value);
		}

		/// <summary>Retourne le nom correspond � la valeur d'une �num�ration</summary>
		public static string? GetName<TEnum>(TEnum value)
			where TEnum : struct, Enum
		{
			return GetName(typeof(TEnum), (Enum)(object)value);
		}

		/// <summary>Retourne le litt�ral (num�rique) correspondant � la valeur d'une �num�ration</summary>
		public static string? GetLiteral(Type enumType, Enum value)
		{
			return GetCacheForType(enumType).GetLiteral(value);
		}

		/// <summary>Retourne le litt�ral (num�rique) correspondant � la valeur d'une �num�ration</summary>
		public static string? GetLiteral<TEnum>(TEnum value)
			where TEnum : struct, Enum
		{
			return GetLiteral(typeof(TEnum), (Enum)(object)value);
		}

		/// <summary>G�n�re le cache correspond � un type d'�num�ration sp�cifique, et ajoute-le au cache global</summary>
		/// <param name="enumType">Type correspond � une Enum</param>
		/// <returns>Cache correspond � cette enum</returns>
		private static EnumStringTable.Cache AddEnumToCache(Type enumType)
		{
			Contract.Debug.Requires(enumType != null);
			if (!typeof(Enum).IsAssignableFrom(enumType)) throw new InvalidOperationException($"Type {enumType.Name} is not a valid Enum type");

			var names = Enum.GetNames(enumType);
			var values = Enum.GetValues(enumType);
			Contract.Debug.Assert(names != null && values != null && names.Length == values.Length);

			var data = new Dictionary<Enum, EnumStringTable.Entry>(names.Length, EqualityComparer<Enum>.Default);
			//note: en cas de doublons de valeurs, on ne doit conserver que la toute premi�re entr�e, pour garder le m�me comportement que ToString()
			// => le plus simple est donc d'it�rer la liste a l'envers, en �crasant les doublons
			for (int i = names.Length - 1; i >= 0; i--)
			{
				var value = (Enum) values.GetValue(i);
				data[value] = new EnumStringTable.Entry(value, value.ToString("D"), names[i]);
			}
			var cache = new EnumStringTable.Cache(enumType, data);

			var sw = new SpinWait();
			while (true)
			{
				// v�rifie si un autre thread n'a pas d�j� g�n�r� le m�me dico...
				var types = EnumStringTable.Types;
				if (types.TryGetValue(enumType, out Cache other)) return other;

				// non, on ajoute le notre
				var update = new Dictionary<Type, Cache>(types, types.Comparer)
				{
					[enumType] = cache
				};
				// et on essaye de publier la nouvelle version du cache global
				if (Interlocked.CompareExchange(ref EnumStringTable.Types, update, types) == types)
				{
					break;
				}
				// on s'est fait doubler par quelqu'un d'autre...
				sw.SpinOnce();
			}

			return cache;
		}

		internal static string CamelCase(string name)
		{
			Contract.Debug.Requires(!string.IsNullOrEmpty(name));

			// check si le premier n'est pas d�j� en minuscules
			char first = name[0];
			if (first == '_' || (first >= 'a' && first <= 'z')) return name;
			// convertir le premier caract�re en minuscules
			var chars = name.ToCharArray();
			chars[0] = char.ToLowerInvariant(first);
			return new string(chars);
		}

	}

}
