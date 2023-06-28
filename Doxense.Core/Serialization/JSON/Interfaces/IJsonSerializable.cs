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

	/// <summary>Interface indiquant qu'un objet peut g�rer lui m�me la s�rialisation en JSON</summary>
	public interface IJsonSerializable
	{
		/// <summary>Transforme l'objet en un string JSON</summary>
		/// <param name="writer">Context courant de la s�rialisation</param>
		void JsonSerialize(CrystalJsonWriter writer);

		/// <summary>D�s�rialise un objet JSON en remplissant l'instance</summary>
		/// <param name="value">Valeur</param>
		/// <param name="declaredType"></param>
		/// <param name="resolver"></param>
		[Obsolete("Use IJsonBindable.JsonUnpack instead")]
		void JsonDeserialize(JsonObject value, Type declaredType, ICrystalJsonTypeResolver resolver);
	}

}
