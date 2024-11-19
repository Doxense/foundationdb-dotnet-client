#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Serialization.Json.CodeGen
{

	/// <summary>Metadata about the container type that will host the generated code for one or more types</summary>
	public sealed record CrystalJsonContainerMetadata
	{
		public required string Name { get; init; }
		
		public required TypeRef Symbol { get; init; }
		
		public required ImmutableEquatableArray<CrystalJsonTypeMetadata> IncludedTypes { get; init; }

	}
	
	/// <summary>Metadata about a serialized type</summary>
	public sealed record CrystalJsonTypeMetadata
	{
		
		/// <summary>Symbol for the serialized type</summary>
		public required TypeRef Symbol { get; init; }

		/// <summary>Friendly name of the type, used as the prefix of the generated converters (ex: "User", "Account", "Order", ...)</summary>
		public string Name => this.Symbol.Name;

		/// <summary>Namespace of the type (ex: "Contoso.Backend.Models")</summary>
		public string NameSpace => this.Symbol.NameSpace;

		/// <summary>For objects, list of included members in this type</summary>
		public required ImmutableEquatableArray<CrystalJsonMemberMetadata> Members { get; init; }
		
	}

	/// <summary>Metadata about a member (field or property) of a serialized type</summary>
	public sealed record CrystalJsonMemberMetadata
	{
		
		/// <summary>Name, as serialized in the JSON output</summary>
		/// <example><c>[JsonProperty("helloWorld")] public string HelloWorld { ... }</c> has name <c>"helloWorld"</c></example>
		public required string Name { get; init; }
		
		/// <summary>Type of the member</summary>
		public required TypeRef Type { get; init; }
		
		/// <summary>Name of the member in the container type</summary>
		/// <example><c>public string HelloWorld { get; init;}</c> has member name <c>"HelloWorld"</c></example>
		public required string MemberName { get; init; }

		/// <summary><c>true</c> if the member is a field, <c>false</c> if it is a property</summary>
		public required bool IsField { get; init; } // true = field, false = prop

		/// <summary><c>true</c> if the member is read-only</summary>
		/// <remarks>For properties, this means there is not SetMethod.</remarks>
		/// <example><c>public string HelloWorld { get; }</c> is read-only</example>
		public required bool IsReadOnly { get; init; }

		/// <summary><c>true</c> if the member is init-only</summary>
		/// <example><c>public string HelloWorld { get; init; }</c> is init-only</example>
		public required bool IsInitOnly { get; init; }
		
		/// <summary><c>true</c> if the member is annotated with the <c>required</c> keyword</summary>
		/// <example><c>public required string Id { ... }</c> is required</example>
		public required bool IsRequired { get; init; } 
		
		/// <summary><c>true</c> if the member is annotated with the <see cref="T:System.ComponentModel.DataAnnotations.KeyAttribute"/> attibute</summary>
		/// <example><c>[Key] public required string Id { get; init; }</c> is marked as part of the key for instances of this type</example>
		public required bool IsKey { get; init; }

	}
	
}
