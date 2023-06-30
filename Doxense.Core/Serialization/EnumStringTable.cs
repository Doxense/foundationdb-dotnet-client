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

namespace Doxense.Serialization
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Helper pour la mise en cache de conversion d'énumération en string</summary>
	[Obsolete("Performance of enum ToString() has been fixed in .NET Core 3.0. This type may not be necessary anymore!")]
	public static class EnumStringTable
	{
		//NOTE: cette classe existe pour palier a un problème de performance du Enum.ToString() dans les versions classique du .NET Framework
		// Les dernières version de .NET Core ont fortement optimisé ce cas, ce qui rend cette classe beaucoup moins indispensable
		// REVIEW: éventuellement la supprimer, si les benchs montrent qu'il n'y a plus de raison de l'utiliser?

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

		/// <summary>Cache contenant les string d'un type d'énumération spécifique</summary>
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

		/// <summary>Retourne le cache correspondant à une énumération spécifique</summary>
		public static EnumStringTable.Cache GetCacheForType(Type enumType)
		{
			var types = EnumStringTable.Types;
			if (!types.TryGetValue(enumType, out Cache cache))
			{
				cache = AddEnumToCache(enumType);
			}
			return cache;
		}

		/// <summary>Retourne le cache correspondant à une énumération spécifique</summary>
		public static EnumStringTable.Cache GetCacheForType<TEnum>()
			where TEnum : struct, Enum
		{
			return GetCacheForType(typeof(TEnum));
		}

		/// <summary>Retourne le nom correspond à la valeur d'une énumération</summary>
		[return: NotNullIfNotNull("value")] 
		public static string? GetName(Type enumType, Enum value)
		{
			return GetCacheForType(enumType).GetName(value);
		}

		/// <summary>Retourne le nom correspond à la valeur d'une énumération</summary>
		public static string? GetName<TEnum>(TEnum value)
			where TEnum : struct, Enum
		{
			return GetName(typeof(TEnum), (Enum)(object)value);
		}

		/// <summary>Retourne le littéral (numérique) correspondant à la valeur d'une énumération</summary>
		public static string? GetLiteral(Type enumType, Enum value)
		{
			return GetCacheForType(enumType).GetLiteral(value);
		}

		/// <summary>Retourne le littéral (numérique) correspondant à la valeur d'une énumération</summary>
		public static string? GetLiteral<TEnum>(TEnum value)
			where TEnum : struct, Enum
		{
			return GetLiteral(typeof(TEnum), (Enum)(object)value);
		}

		/// <summary>Génère le cache correspond à un type d'énumération spécifique, et ajoute-le au cache global</summary>
		/// <param name="enumType">Type correspond à une Enum</param>
		/// <returns>Cache correspond à cette enum</returns>
		private static EnumStringTable.Cache AddEnumToCache(Type enumType)
		{
			Contract.Debug.Requires(enumType != null);
			if (!typeof(Enum).IsAssignableFrom(enumType)) throw new InvalidOperationException($"Type {enumType.Name} is not a valid Enum type");

			var names = Enum.GetNames(enumType);
			var values = Enum.GetValues(enumType);
			Contract.Debug.Assert(names != null && values != null && names.Length == values.Length);

			var data = new Dictionary<Enum, EnumStringTable.Entry>(names.Length, EqualityComparer<Enum>.Default);
			//note: en cas de doublons de valeurs, on ne doit conserver que la toute première entrée, pour garder le même comportement que ToString()
			// => le plus simple est donc d'itérer la liste a l'envers, en écrasant les doublons
			for (int i = names.Length - 1; i >= 0; i--)
			{
				var value = (Enum) values.GetValue(i);
				data[value] = new EnumStringTable.Entry(value, value.ToString("D"), names[i]);
			}
			var cache = new EnumStringTable.Cache(enumType, data);

			var sw = new SpinWait();
			while (true)
			{
				// vérifie si un autre thread n'a pas déjà généré le même dico...
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

			// check si le premier n'est pas déjà en minuscules
			char first = name[0];
			if (first == '_' || (first >= 'a' && first <= 'z')) return name;
			// convertir le premier caractère en minuscules
			var chars = name.ToCharArray();
			chars[0] = char.ToLowerInvariant(first);
			return new string(chars);
		}

	}

}
