#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	public readonly struct JsonObservableProxyDictionary<TValue, TProxy>
		where TProxy : IJsonObservableProxy<TValue, TProxy>
	{

		private readonly ObservableJsonValue m_obj;

		public JsonObservableProxyDictionary(ObservableJsonValue array)
		{
			m_obj = array;
		}

		public ObservableJsonValue GetValue() => m_obj;

		public TProxy this[string key] => TProxy.Create(m_obj[key]);

		public TProxy this[ReadOnlyMemory<char> key] => TProxy.Create(m_obj[key]);

		public TProxy this[JsonPath path] => TProxy.Create(m_obj[path]);

	}

}
