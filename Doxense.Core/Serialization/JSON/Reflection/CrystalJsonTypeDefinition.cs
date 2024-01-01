#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Collections.Generic;
	using System.Diagnostics;
	using Doxense.Diagnostics.Contracts;

	[DebuggerDisplay("Type={Type.Name}, ReqClass={RequiresClassAttribute}, IsAnonymousType={IsAnonymousType}, ClassId={ClassId}")]
	public record CrystalJsonTypeDefinition : ICrystalTypeDefinition
	{
		public Type Type { get; init; }

		public Type? BaseType { get; init; }

		public string? ClassId { get; init; }

		public bool RequiresClassAttribute { get; init; }

		public bool IsAnonymousType { get; init; }

		public Func<object>? Generator { get; init; }

		public CrystalJsonTypeBinder? CustomBinder { get; init; }

		public CrystalJsonMemberDefinition[] Members { get; init; }

		/// <summary>Nouvelle définition de type</summary>
		/// <param name="type">Type représenté</param>
		/// <param name="baseType"></param>
		/// <param name="classId">Identifiant custom du type (si null, génère un identifiant "Namespace.ClassName, Assembly"</param>
		/// <param name="customBinder"></param>
		/// <param name="generator">Fonction capable de créer un nouvel objet de ce type (ou null si ce n'est pas possible, comme pour des interfaces ou des classes abstraites)</param>
		/// <param name="members">Définitions des membres de ce type</param>
		public CrystalJsonTypeDefinition(Type type, Type? baseType, string? classId, CrystalJsonTypeBinder? customBinder, Func<object>? generator, CrystalJsonMemberDefinition[] members)
		{
			Contract.NotNull(type);
			Contract.NotNull(members);

			if (classId == null)
			{
				// Récupère le nom du type sous la forme "Namespace.ClassName, AssemblyName" pour pouvoir l'utiliser avec Type.GetType(..)
				classId = type.GetAssemblyName();
			}

			this.Type = type;
			this.BaseType = baseType;
			this.ClassId = classId;
			this.IsAnonymousType = type.IsAnonymousType();
			this.RequiresClassAttribute = (type.IsInterface || type.IsAbstract) && !this.IsAnonymousType && baseType == null;
			this.CustomBinder = customBinder;
			this.Generator = generator;
			this.Members = members;
		}

		IReadOnlyList<ICrystalMemberDefinition> ICrystalTypeDefinition.Members => this.Members;

	}

}
