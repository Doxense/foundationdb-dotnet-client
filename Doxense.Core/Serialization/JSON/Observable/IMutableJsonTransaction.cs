#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	/// <summary>Transaction that will record all the changes made to an <see cref="MutableJsonValue"/></summary>
	public interface IMutableJsonTransaction
	{

		/// <summary>Tests if the underlying observed object has been changed during this transaction</summary>
		/// <remarks>Returns <c>false</c> if no changes have been made, or if alls changes did not actually mutate the value.</remarks>
		bool HasMutations { get; }

		/// <summary>Number of changes registered on this transaction</summary>
		int Count { get; }

		/// <summary>Reset the transaction to its initial state, reverting any previous mutations.</summary>
		/// <remarks>This method can be used to reuse the current transaction instance</remarks>
		void Reset();

		MutableJsonValue FromJson(JsonValue value);

		MutableJsonValue FromJson(MutableJsonValue parent, JsonPathSegment path, JsonValue? value);

		/// <summary>Creates a new empty object, using the transactions default settings</summary>
		JsonObject NewObject();

		/// <summary>Creates a new empty array, using the transactions default settings</summary>
		JsonArray NewArray();

		/// <summary>Records the addition of a new field on an object</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="key">Name of the field that was added</param>
		/// <param name="argument">Value of the new field</param>
		/// <remarks>
		/// <para>This records the facts that a new field is added to an object, OR that a field previously set to <c>null</c> now has a non-null value.</para>
		/// </remarks>
		void RecordAdd(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument);

		/// <summary>Truncate an array to the specified length</summary>
		/// <param name="instance">Parent instance (expected to be an array)</param>
		/// <param name="length">New length of the array</param>
		/// <remarks>
		/// <para>Any item that would fall outside the new bounds of the array will be removed.</para>
		/// <para>If the array is smaller than <paramref name="length"/>, extra <c>null</c> entries will be appended as needed.</para>
		/// </remarks>
		void RecordTruncate(IJsonProxyNode instance, int length);

		/// <summary>Records the update of an existing field of an object</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="key">Name of the field that was updated</param>
		/// <param name="argument">Updated value of the field</param>
		/// <remarks>
		/// <para>This records the fact that the value of a field of the object as been replaced by another value.</para>
		/// <para>Any previous mutation on this field, or any child, is superseded by this record</para>
		/// </remarks>
		void RecordUpdate(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument);

		/// <summary>Records the update of an existing field of an object, using a patch definition</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="key">Name of the field that is being updated</param>
		/// <param name="argument">Patch that describes the changes to this field</param>
		/// <remarks>
		/// <para>This records the fact that the value of a field of the object as been patched.</para>
		/// <para>Any previous mutation on this field, or any child, should be merged with this patch</para>
		/// </remarks>
		void RecordPatch(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument);

		/// <summary>Records the deletion of an existing field of an object</summary>
		/// <param name="instance">Parent instance (expected to be an object)</param>
		/// <param name="key">Name of the field that is being removed</param>
		/// <remarks>
		/// <para>This records the fact that the field does not exist any longer in the object</para>
		/// <para>Setting a field to null is logically equivalent to deleting the field</para>
		/// <para>Any previous mutation on this item, or any child, should be superseded by this record.</para>
		/// </remarks>
		void RecordDelete(IJsonProxyNode instance, JsonPathSegment child);

		/// <summary>Records the removal of all fields in object, or items in an array</summary>
		/// <param name="instance">Parent instance (expected to be an either an object or array)</param>
		/// <remarks>
		/// <para>The object or array instance will we cleared of any content.</para>
		/// </remarks>
		void RecordClear(IJsonProxyNode instance);

	}

}
