#region Copyright Doxense 2010-2014
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;

	/// <summary>Structure contenant les infos sur un field ou une property d'un objet</summary>
	public interface ICrystalMemberDefinition
	{
		string Name { get; }

		Type Type { get; }

		object? DefaultValue { get; }

		bool ReadOnly { get; }

		Func<object, object> Getter { get; }

		Action<object, object>? Setter { get; }

	}

}
