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
		//note: l'ordre des valeurs influe sur l'ordre de tri de JsonValue de type diff�rents (sauf null qui est toujours dernier)

		/// <summary>Repr�sente une valeur null, ou manquante. i.e: l'absence de valeur</summary>
		Null,
		/// <summary>True, ou False.</summary>
		Boolean,
		/// <summary>Nombre entier, ou d�cimal</summary>
		Number,
		/// <summary>[EXTENSION] Repr�sente une date (sous forme de <see cref="String"/>). Utilis� uniquement pour optimiser certaines op�rations</summary>
		DateTime,
		/// <summary>Repr�sente une cha�ne de texte (�ventuellement vide, mais pas null)</summary>
		String,
		/// <summary>Repr�sente une liste de valeurs JSON (�ventuellement vide, mais pas null)</summary>
		Array,
		/// <summary>Repr�sente une objet JSON (�ventuellement vide, mais pas null)</summary>
		Object,
	}

}
