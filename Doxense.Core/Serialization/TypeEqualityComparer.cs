#region Copyright Doxense 2010-2020
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization
{
	using System;
	using System.Collections.Generic;

	/// <summary>Equality comparer spécialisé dans la comparaison de types</summary>
	public sealed class TypeEqualityComparer : IEqualityComparer<Type>
	{
		// La class 'Type' à une méthode Equals(Type), mais n'implémente pas IEquatable<Type> donc elle n'est pas vue par EqualityComparer<Type>.Default  :(

		public static readonly IEqualityComparer<Type> Default = new TypeEqualityComparer();

		private TypeEqualityComparer()
		{ }

		public bool Equals(Type x, Type y)
		{
			return object.ReferenceEquals(x, y);
		}

		public int GetHashCode(Type obj)
		{
			return obj?.GetHashCode() ?? -1;
		}

	}

}
