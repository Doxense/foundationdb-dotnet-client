#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
