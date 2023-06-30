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

	/// <summary>Resolveur capable d'énumérer les membres d'un type</summary>
	public interface ICrystalTypeResolver
	{
		ICrystalTypeDefinition? ResolveType(Type type);

		Type? ResolveClassId(string classId);
	}

}
