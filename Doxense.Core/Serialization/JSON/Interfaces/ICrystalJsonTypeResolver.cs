#region Copyright 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;

	/// <summary>Resolveur JSON capable d'énumérer les membres d'un type</summary>
	public interface ICrystalJsonTypeResolver : ICrystalTypeResolver
	{
		/// <summary>Inspecte un type pour retrouver la liste de ses membres</summary>
		/// <param name="type">Type à inspecter</param>
		/// <returns>Liste des members compilée, ou null si le type n'est pas compatible (primitive, delegate, ...)</returns>
		/// <remarks>La liste est mise en cache pour la prochaine fois</remarks>
		CrystalJsonTypeDefinition? ResolveJsonType(Type type);

		/// <summary>Bind une valeur JSON en type CLR correspondant (ValueType, Class, List, ...)</summary>
		object? BindJsonValue(Type? type, JsonValue? value);

		/// <summary>Bind un objet JSON en type CLR</summary>
		object? BindJsonObject(Type? type, JsonObject? value);

		/// <summary>Bind un liste JSON en liste d'objets CLR</summary>
		object? BindJsonArray(Type? type, JsonArray? array);

		/// <summary>Bind une valeur JSON en un type CLR spécifique</summary>
		T? BindJson<T>(JsonValue? value);

	}

}
