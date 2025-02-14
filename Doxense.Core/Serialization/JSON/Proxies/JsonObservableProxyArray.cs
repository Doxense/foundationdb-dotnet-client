#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	public readonly struct JsonObservableProxyArray<TValue, TProxy>
		where TProxy : IJsonObservableProxy<TValue, TProxy>
	{

		private readonly ObservableJsonValue m_array;

		public JsonObservableProxyArray(ObservableJsonValue array)
		{
			m_array = array;
		}

		public ObservableJsonValue GetValue() => m_array;

		public TProxy this[int index] => TProxy.Create(m_array[index]);

		public TProxy this[Index index] => TProxy.Create(m_array[index]);

	}

}
