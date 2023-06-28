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

	/// <summary>Delegate appelé pour sérialiser une objet en JSON</summary>
	/// <param name="value">Valeur (managée) à sérialiser</param>
	/// <param name="declaringType">Type du membre parent (dans le cas d'une membre d'un objet, d'un item d'une collection, ...)</param>
	/// <param name="runtimeType">Type réel de l'objet au runtime (ou null lors de la compilation d'une définition de type</param>
	/// <param name="writer">Contexte de la sérialisation</param>
	public delegate void CrystalJsonTypeVisitor(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer);

}
