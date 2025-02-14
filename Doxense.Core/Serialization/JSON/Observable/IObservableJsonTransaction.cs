#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	public interface IObservableJsonTransaction
	{

		/// <summary>Tests if the underlying observed object has been changed during this transaction</summary>
		/// <remarks>Returns <c>false</c> if no changes have been made, or if alls changes did not actually mutate the value.</remarks>
		bool HasMutations { get; }

		/// <summary>Number of changes registered on this transaction</summary>
		int Count { get; }

		ObservableJsonValue NewObject(ObservableJsonPath path);

		ObservableJsonValue NewArray(ObservableJsonPath path);

		ObservableJsonValue FromJson(ObservableJsonValue parent, string key, JsonValue? value);

		ObservableJsonValue FromJson(ObservableJsonValue parent, int index, JsonValue? value);

		ObservableJsonValue FromJson(ObservableJsonValue parent, Index index, JsonValue? value);

		void RecordAdd(ObservableJsonValue instance, ReadOnlySpan<char> key, JsonValue argument);

		void RecordAdd(ObservableJsonValue instance, int index, JsonValue argument);

		void RecordUpdate(ObservableJsonValue instance, ReadOnlySpan<char> key, JsonValue argument);

		void RecordUpdate(ObservableJsonValue instance, int index, JsonValue argument);

		void RecordPatch(ObservableJsonValue instance, ReadOnlySpan<char> key, JsonValue argument);

		void RecordPatch(ObservableJsonValue instance, int index, JsonValue argument);

		void RecordDelete(ObservableJsonValue instance, ReadOnlySpan<char> key);

		void RecordDelete(ObservableJsonValue instance, int index);

		void RecordClear(ObservableJsonValue instance);

		void Reset();

	}

}
