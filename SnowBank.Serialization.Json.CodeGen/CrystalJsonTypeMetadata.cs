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

namespace SnowBank.Serialization.Json.CodeGen
{
	using System.Text;

	/// <summary>Metadata about the container type that will host the generated code for one or more types</summary>
	public sealed record CrystalJsonContainerMetadata
	{

		/// <summary>Name of the container</summary>
		public required string Name { get; init; }
		
		/// <summary>Type of the container</summary>
		public required TypeMetadata Type { get; init; }

		/// <summary>List of all application types that will be part of the generated source code</summary>
		public required ImmutableEquatableArray<CrystalJsonTypeMetadata> IncludedTypes { get; init; }

		//TODO: settings!

	}
	
	/// <summary>Metadata about a serialized type</summary>
	public sealed record CrystalJsonTypeMetadata
	{
		
		/// <summary>Symbol for the serialized type</summary>
		public required TypeMetadata Type { get; init; }

		/// <summary>Friendly name of the type, used as the prefix of the generated converters (ex: "User", "Account", "Order", ...)</summary>
		public string Name => this.Type.Name;

		/// <summary>For objects, list of included members in this type</summary>
		public required ImmutableEquatableArray<CrystalJsonMemberMetadata> Members { get; init; }

		public void Explain(StringBuilder sb, string? indent = null)
		{
			sb.Append(indent).Append("Name = ").AppendLine(this.Name);
			sb.Append(indent).Append("Type = ").AppendLine(this.Type.Ref.ToString());
			this.Type.Explain(sb, indent is null ? "- " : ("  " + indent));
			sb.Append(indent).Append("Members = [").Append(this.Members.Count).AppendLine("]");
			var subIndent = indent is null ? "- " : ("  " + indent);
			var subIndent2 = ("  " + subIndent);
			foreach (var member in this.Members)
			{
				sb.Append(subIndent).AppendLine(member.Name);
				member.Explain(sb, subIndent2);
			}
		}

	}

	/// <summary>Metadata about a member (field or property) of a serialized type</summary>
	public sealed record CrystalJsonMemberMetadata
	{
		
		/// <summary>Name, as serialized in the JSON output</summary>
		/// <example><c>[JsonProperty("helloWorld")] public string HelloWorld { ... }</c> has name <c>"helloWorld"</c></example>
		public required string Name { get; init; }
		
		/// <summary>Type of the member</summary>
		public required TypeMetadata Type { get; init; }
		
		/// <summary>Name of the member in the container type</summary>
		/// <example><c>public string HelloWorld { get; init;}</c> has member name <c>"HelloWorld"</c></example>
		public required string MemberName { get; init; }

		public required ImmutableEquatableArray<string> Attributes { get; init; } //HACKHACK: TEMP: to be able to diagnose attribute stuff!

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

		/// <summary>If not-null, this is the expression that represents the default value for this member, when it is missing</summary>
		/// <remarks>This should be a valid C# constant expression, like <c>123</c>, <c>"hello"</c>, <c>true</c>, ...</remarks>
		public required string DefaultLiteral { get; init; }

		/// <summary>The member has the <see cref="T:System.ComponentModel.DataAnnotations.KeyAttribute"/> attribute</summary>
		/// <remarks>Examples: <code>
		/// int Id { get; ... } // IsKey == false
		/// 
		/// [Key]
		/// int Id { get; ... } // IsKey == true
		/// </code></remarks>
		public required bool IsKey { get; init; }

		/// <summary>The member cannot be null, or is annotated with <see cref="T:System.Diagnostics.CodeAnalysis.NotNullAttribute"/></summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }     // IsNotNull == true
		/// int? Foo { get; ... }    // IsNotNull == false
		/// string Foo { get; ... }  // IsNotNull == true
		/// string? Foo { get; ... } // IsNotNull == false
		/// </code></remarks>
		public required bool IsNotNull { get; init; }

		/// <summary>The member if a reference type that is declared as nullable in its parent type</summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }     // IsNullableRefType == false
		/// int? Foo { get; ... }    // IsNullableRefType == false
		/// string Foo { get; ... }  // IsNullableRefType == false
		/// string? Foo { get; ... } // IsNullableRefType == true
		/// </code></remarks>
		public bool IsNullableRefType() => !this.IsNotNull && this.Type.NullableOfType is null;

		public void Explain(StringBuilder sb, string? indent = null)
		{
			sb.Append(indent).Append("Name = ").AppendLine(this.Name);
			sb.Append(indent).Append("MemberName = ").AppendLine(this.MemberName);
			if (this.IsField) sb.Append(indent).AppendLine("IsField = true");
			if (this.IsNotNull) sb.Append(indent).AppendLine("IsNotNull = true");
			if (this.IsReadOnly) sb.Append(indent).AppendLine("IsReadOnly = true");
			if (this.IsInitOnly) sb.Append(indent).AppendLine("IsInitOnly = true");
			if (this.IsRequired) sb.Append(indent).AppendLine("IsRequired = true");
			if (this.IsKey) sb.Append(indent).AppendLine("IsKey = true");
			if (this.DefaultLiteral is not ("null" or "default")) sb.Append(indent).Append("DefaultValue = ").AppendLine(this.DefaultLiteral);
			var subIndent = indent is null ? "- " : ("  " + indent);
			sb.Append(indent).AppendLine("Attributes:");
			foreach (var attr in this.Attributes)
			{
				sb.Append(subIndent).AppendLine(attr);
			}
			sb.Append(indent).AppendLine("Type:");
			this.Type.Explain(sb, subIndent);
		}

	}
	
}
