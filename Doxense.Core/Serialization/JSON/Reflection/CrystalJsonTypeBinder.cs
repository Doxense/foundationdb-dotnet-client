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

	/// <summary>Delegate appelé pour désérialiser une valeur JSON en objet managé</summary>
	/// <param name="value">Valeur JSON à binder</param>
	/// <param name="bindingType">Type managé attendu</param>
	/// <param name="resolver">Résolveur de type</param>
	/// <returns>Objet managé désérialisé</returns>
	public delegate object? CrystalJsonTypeBinder(JsonValue? value, Type bindingType, ICrystalJsonTypeResolver resolver);

}
