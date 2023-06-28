#region Copyright Doxense 2010-2021
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;

	/// <summary>Type d'une valeur JSON (String, Number, ...)</summary>
	public enum JsonType
	{
		//note: l'ordre des valeurs influe sur l'ordre de tri de JsonValue de type différents (sauf null qui est toujours dernier)

		/// <summary>Représente une valeur null, ou manquante. i.e: l'absence de valeur</summary>
		Null,
		/// <summary>True, ou False.</summary>
		Boolean,
		/// <summary>Nombre entier, ou décimal</summary>
		Number,
		/// <summary>[EXTENSION] Représente une date (sous forme de <see cref="String"/>). Utilisé uniquement pour optimiser certaines opérations</summary>
		DateTime,
		/// <summary>Représente une chaîne de texte (éventuellement vide, mais pas null)</summary>
		String,
		/// <summary>Représente une liste de valeurs JSON (éventuellement vide, mais pas null)</summary>
		Array,
		/// <summary>Représente une objet JSON (éventuellement vide, mais pas null)</summary>
		Object,
	}

}
