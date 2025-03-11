#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	/// <summary>Context that will record all the reads performed on a <see cref="ObservableReadOnlyJsonValue"/></summary>
	public interface IObservableJsonContext
	{

		/// <summary>Reset the context to its initial state, reverting any previous mutations.</summary>
		/// <remarks>This method can be used to reuse the current context for a different session</remarks>
		void Reset();

		ObservableReadOnlyJsonValue FromJson(JsonValue value);

		ObservableReadOnlyJsonValue FromJson(ObservableReadOnlyJsonValue? parent, ReadOnlyMemory<char> key, JsonValue value);

		ObservableReadOnlyJsonValue FromJson(ObservableReadOnlyJsonValue? parent, Index index, JsonValue value);

		/// <summary>Records the access to a field of an object</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="key">Name of the field that was accessed</param>
		/// <param name="argument">Current value of the field, or <see cref="JsonNull.Missing"/> if the field does not exist in its parent</param>
		/// <param name="existOnly">If <c>true</c>, only the existence (or absence) was used, and not the value itself</param>
		/// <remarks>
		/// <para>If <paramref name="argument"/> is <see cref="JsonNull.Null"/>, it means the field is present in the object, but with a <c>null</c> value.</para>
		/// <para>If <paramref name="argument"/> is <see cref="JsonNull.Missing"/>, it means the field is not present in the object.</para>
		/// <para>If <paramref name="key"/> is empty, it means the itself was accessed</para>
		/// </remarks>
		void RecordRead(ObservableReadOnlyJsonValue instance, ReadOnlyMemory<char> key, JsonValue argument, bool existOnly);

		/// <summary>Records the access to an item of an array</summary>
		/// <param name="instance">Parent instance (expected to be an array)</param>
		/// <param name="index">Index of the item that was accessed</param>
		/// <param name="argument">Current value of the item, or <see cref="JsonNull.Error"/> if the field is outside the bounds of its parent</param>
		/// <param name="existOnly">If <c>true</c>, only the existence (or absence) was used, and not the value itself</param>
		/// <remarks>
		/// <para>If <paramref name="argument"/> is <see cref="JsonNull.Null"/>, it means the index is inside the array, but the corresponding item has a <c>null</c> value.</para>
		/// <para>If <paramref name="argument"/> is <see cref="JsonNull.Error"/>, it means the index is outside the bounds of the array.</para>
		/// <para>If <paramref name="index"/> is equal to <c>^0</c>, it means the length of the array was accessed, but not the contents of the array.</para>
		/// </remarks>
		void RecordRead(ObservableReadOnlyJsonValue instance, Index index, JsonValue argument, bool existOnly);

		/// <summary>Records the fact that the length of an array was accessed</summary>
		void RecordLength(ObservableReadOnlyJsonValue instance, JsonValue argument);

	}

}
