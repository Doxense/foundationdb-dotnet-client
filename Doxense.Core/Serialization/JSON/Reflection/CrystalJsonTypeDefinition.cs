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
