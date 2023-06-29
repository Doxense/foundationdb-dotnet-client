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

	public interface IJsonPackable
	{
		/// <summary>Transforme l'objet en une JsonValue</summary>
		/// <param name="settings">Settings utilis�s pour la s�rialisation</param>
		/// <param name="resolver">Resolver optionnel</param>
		JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver);
	}


	/// <summary>Interface indiquant qu'un objet peut g�rer lui m�me la s�rialisation vers/depuis un DOM JSON</summary>
	public interface IJsonBindable : IJsonPackable
	{
		/// <summary>D�s�rialise un objet JSON en remplissant l'instance</summary>
		/// <param name="value">Valeur JSON pars�e � binder</param>
		/// <param name="resolver">Resolver optionnel</param>
		void JsonUnpack(JsonValue value, ICrystalJsonTypeResolver resolver);
	}

}
