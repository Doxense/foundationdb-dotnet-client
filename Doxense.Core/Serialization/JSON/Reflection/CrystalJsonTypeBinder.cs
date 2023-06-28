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

	/// <summary>Delegate appel� pour d�s�rialiser une valeur JSON en objet manag�</summary>
	/// <param name="value">Valeur JSON � binder</param>
	/// <param name="bindingType">Type manag� attendu</param>
	/// <param name="resolver">R�solveur de type</param>
	/// <returns>Objet manag� d�s�rialis�</returns>
	public delegate object? CrystalJsonTypeBinder(JsonValue? value, Type bindingType, ICrystalJsonTypeResolver resolver);

}
