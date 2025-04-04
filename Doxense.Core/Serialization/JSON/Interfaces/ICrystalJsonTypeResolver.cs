#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Serialization.Json
{
	/// <summary>Type that is able to extra type metadata used during JSON serialization</summary>
	[PublicAPI]
	public interface ICrystalJsonTypeResolver
	{

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		Type? ResolveClassId(string classId);

		/// <summary>Inspects a type, and generate a list of all its members</summary>
		/// <param name="type">Type to introspect</param>
		/// <returns>List of compiled members, or <see langword="null"/> if the type is not compatible (primitive, delegate, ...)</returns>
		/// <remarks>The list is computed on the first call for each type, and then cached in memory for subsequent calls</remarks>
		CrystalJsonTypeDefinition? ResolveJsonType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type);

		/// <summary>Returns the definition of a member of a type</summary>
		/// <param name="type">Type that contains the member</param>
		/// <param name="memberName">Name of the member</param>
		/// <returns>If known, the definition for this member.</returns>
		/// <remarks>This is useful to inspect the custom serialization settings for a particular member of a type, that can be overriden, for example, by <see cref="JsonPropertyAttribute"/></remarks>
		CrystalJsonMemberDefinition? ResolveMemberOfType(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
			Type type,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
			string memberName
		);

		/// <summary>Binds a JSON value into the corresponding CLR type</summary>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		object? BindJsonValue(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
			Type? type,
			JsonValue? value);

		/// <summary>Binds a JSON value into the corresponding CLR type</summary>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[return: NotNullIfNotNull(nameof(defaultValue))]
		T? BindJson<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
			(JsonValue? value, T? defaultValue = default);

		/// <summary>Binds a JSON object into the corresponding CLR type</summary>
		/// <exception cref="JsonBindingException">If the object cannot be bound to the specified type.</exception>
		object? BindJsonObject(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]Type? type,
			JsonObject? value);

		/// <summary>Binds a JSON array into the corresponding CLR type</summary>
		/// <exception cref="JsonBindingException">If the array cannot be bound to the specified type.</exception>
		object? BindJsonArray(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
			Type? type,
			JsonArray? array);

	}

}
