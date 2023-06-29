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
	using System.Collections.Generic;
	using Doxense.Diagnostics.Contracts;

	public sealed class JsonValueComparer : IEqualityComparer<JsonValue>, IComparer<JsonValue>, System.Collections.IEqualityComparer, System.Collections.IComparer
	{
		public static readonly JsonValueComparer Default = new JsonValueComparer();

		private JsonValueComparer()
		{ }

		public bool Equals(JsonValue? x, JsonValue? y)
		{
			// il y a pas mal de singletons ou de valeurs interned, donc ca vaut le coup de comparer les pointers
			return object.ReferenceEquals(x, y)
			   || (object.ReferenceEquals(x, null) ? y!.IsNull : x.Equals(y));
		}

		public int GetHashCode(JsonValue? obj)
		{
			return (obj ?? JsonNull.Null).GetHashCode();
		}

		bool System.Collections.IEqualityComparer.Equals(object? x, object? y)
		{
			if (object.ReferenceEquals(x, y)) return true; // catch aussi le null
			var jx = (x as JsonValue) ?? JsonValue.FromValue(x);
			var jy = (y as JsonValue) ?? JsonValue.FromValue(y);
			Contract.Debug.Assert(!object.ReferenceEquals(jx, null) && !object.ReferenceEquals(jy, null));
			return jx.Equals(jy);
		}

		int System.Collections.IEqualityComparer.GetHashCode(object? obj)
		{
			return (obj ?? JsonNull.Null).GetHashCode();
		}

		public int Compare(JsonValue? x, JsonValue? y)
		{
			return object.ReferenceEquals(x, y) ? 0 : (x ?? JsonNull.Null).CompareTo(y);
		}

		int System.Collections.IComparer.Compare(object? x, object? y)
		{
			if (object.ReferenceEquals(x, y)) return 0; // catch aussi le null
			var jx = (x as JsonValue) ?? JsonValue.FromValue(x);
			var jy = (y as JsonValue) ?? JsonValue.FromValue(y);
			Contract.Debug.Assert(!object.ReferenceEquals(jx, null) && !object.ReferenceEquals(jy, null));
			return jx.CompareTo(jy);
		}
	}

}
