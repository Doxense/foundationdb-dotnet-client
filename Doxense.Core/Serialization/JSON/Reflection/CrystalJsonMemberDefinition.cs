#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Diagnostics;

	/// <summary>Structure contenant les infos sur un field ou une property d'un objet</summary>
	[DebuggerDisplay("Name={Name}, Type={Type}")]
	public record CrystalJsonMemberDefinition : ICrystalMemberDefinition
	{
		/// <summary>Nom</summary>
		public string Name { get; init; }

		/// <summary>Type de retour</summary>
		public Type Type { get; init; }

		/// <summary>Attribut <see cref="JsonPropertyAttribute"/> appliqué sur le champ (optionnel)</summary>
		public JsonPropertyAttribute? Attributes { get; init; }

		/// <summary>Valeur considérée "par défaut"</summary>
		public object? DefaultValue { get; init; }

		/// <summary>Champ non modifiable (probablement calculé à partir d'autres champs)</summary>
		public bool ReadOnly { get; init; }

		/// <summary>Function capable de retourner la valeur de ce champ</summary>
		public Func<object, object?> Getter { get; init; }

		/// <summary>Function capable de fixer la valeur de ce champ</summary>
		public Action<object, object?>? Setter { get; init; }

		/// <summary>Function capable de sérialiser ce champ directement</summary>
		public CrystalJsonTypeVisitor Visitor { get; init; }

		/// <summary>Function capable de transformer la valeur de base JSON en le bon type</summary>
		public CrystalJsonTypeBinder Binder { get; init; }

		public bool IsDefaultValue(object? value)
		{
			return this.DefaultValue?.Equals(value) ?? (value is null);
		}

	}

}
