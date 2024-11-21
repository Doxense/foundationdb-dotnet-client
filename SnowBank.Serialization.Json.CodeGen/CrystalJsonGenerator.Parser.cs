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

//#define FULL_DEBUG

namespace Doxense.Serialization.Json.CodeGen
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Threading;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	
	public partial class CrystalJsonSourceGenerator
	{

		/// <summary>Parse the symbols from the compilation, in order to extract metadata for the serialization of application types</summary>
		internal sealed class Parser
		{


			public const string RequiredMemberAttributeFullName = "System.Runtime.CompilerServices.RequiredMemberAttribute";
			
			/// <summary>Table of known symbols from this compilation</summary>
			private KnownTypeSymbols KnownSymbols { get; }
			
			public Parser(KnownTypeSymbols knownSymbols)
			{
				this.KnownSymbols = knownSymbols;
			}

			public CrystalJsonContainerMetadata? ParseContainerMetadata(ClassDeclarationSyntax contextClassDeclaration, SemanticModel semanticModel, ImmutableArray<AttributeData> attributes, CancellationToken cancellationToken)
			{
				// we are inspecting the "container" type that will host all the generated serialization code
				// - the container should be a partial class
				// - it should have the [CrystalJsonConverter] attribute applied to it (which is the marker for triggering this code generator)
				// - it should have one or more [CrystalJsonSerializable(typeof(...))] attributes for each of the "root" application types to serialize
				
				var symbol = semanticModel.GetDeclaredSymbol(contextClassDeclaration, cancellationToken);
				if (symbol == null) return null;
				
				Kenobi($"ParseContainerMetadata({symbol.Name}, [{attributes.Length}])");

				var converterAttribute = attributes[0];
				//TODO: extract some settings from this?

				var includedTypes = new List<CrystalJsonTypeMetadata>();
				foreach (var typeAttribute in symbol.GetAttributes())
				{
					var ac = typeAttribute.AttributeClass;
					if (ac == null) continue;

					if (ac.ToDisplayString() != CrystalJsonSerializableAttributeFullName)
					{
						continue;
					}

					if (typeAttribute.ConstructorArguments.Length < 1)
					{
						continue;
					}

					if (typeAttribute.ConstructorArguments[0].Value is not INamedTypeSymbol type)
					{
						continue;
					}

					Kenobi($"Include type {type}");
					try
					{
						var typeDef = ParseTypeMetadata(type, typeAttribute);
						if (typeDef != null)
						{
							includedTypes.Add(typeDef);
						}
					}
					catch (Exception ex)
					{
						Kenobi($"CRASH for {type}: {ex.ToString()}");
					}
				}

				var containerName = symbol.Name;
				
				return new()
				{
					Name = containerName,
					Type = TypeMetadata.Create(symbol),
					IncludedTypes = includedTypes.ToImmutableEquatableArray(),
				};
			}

			public CrystalJsonTypeMetadata? ParseTypeMetadata(INamedTypeSymbol type, AttributeData? attribute)
			{
				// we have to extract all the properties that will be required later during the code generation phase
				
				// does the type implement IJsonSerializable or IJsonPackable?
				var x = type.FindImplementationForInterfaceMember(this.KnownSymbols.IJsonSerializable);
				var y = type.FindImplementationForInterfaceMember(this.KnownSymbols.IJsonPackable);

				var members = new List<CrystalJsonMemberMetadata>();

				foreach (var member in type.GetMembers())
				{
					if (member.Kind is SymbolKind.Property or SymbolKind.Field or SymbolKind.Method)
					{
						var memberDef = this.ParseMemberMetadata(type, member);
						if (memberDef != null)
						{
							members.Add(memberDef);
						}
					}
				}
				
				return new()
				{
					Type = TypeMetadata.Create(type),
					Members = members.ToImmutableEquatableArray(),
				};
			}

			public CrystalJsonMemberMetadata? ParseMemberMetadata(INamedTypeSymbol containerType, ISymbol member)
			{
				var memberName = member.Name;
				bool isField;
				ITypeSymbol typeSymbol;
				bool isReadOnly;
				bool isInitOnly;

				switch (member)
				{
					case IPropertySymbol property:
					{
						if (property.IsImplicitlyDeclared || property.DeclaredAccessibility is (Accessibility.Private or Accessibility.Protected))
						{
							// REVIEW: TODO: what should we do with private properties?
							// - if they have a backing field, the object may not incomplete when deserialized
							return null;
						}
						isField = false;
						typeSymbol = property.Type;
						isReadOnly = property.IsReadOnly;
						isInitOnly = false; //TODO: detect "IsExternalInit" modifier!
						break;
					}
					case IFieldSymbol field:
					{
						if (field.IsImplicitlyDeclared || field.DeclaredAccessibility is (Accessibility.Private or Accessibility.Protected))
						{
							//note: we see the backing fields here, we could maybe capture them somewhere in order to generate optimized unsafe accessors?return null;
							return null;
						}
						isField = true;
						typeSymbol = field.Type;
						isReadOnly = field.IsReadOnly;
						isInitOnly = false; //TODO: detect "IsExternalInit" modifier!
						break;
					}
					default:
					{
						return null;
					}
				}

				var type = TypeMetadata.Create(typeSymbol);

				bool isNotNull;
				if (type.IsValueType())
				{
					isNotNull = type.UnderlyingType == null;
				}
				else
				{
					isNotNull = type.Nullability == NullableAnnotation.Annotated;
				}

				// parameters that can be modified via attributes or keywords on the member
				var name = memberName;
				bool isRequired = false;
				bool isKey = false;
				
				foreach (var attribute in member.GetAttributes())
				{
					var attributeType = attribute.AttributeClass;
					if (attributeType is null) continue;

					switch (attributeType.ToDisplayString())
					{
						case KnownTypeSymbols.JsonPropertyAttributeFullName:
						{
							if (attribute.ConstructorArguments.Length > 0)
							{
								name = (string) attribute.ConstructorArguments[0].Value!;
							}
							break;
						}
						case "System.ComponentModel.DataAnnotations.KeyAttribute":
						{
							isKey = true;
							break;
						}
						case RequiredMemberAttributeFullName:
						{
							isRequired = true;
							break;
						}
					}
				}
				
				return new()
				{
					Type = type,
					Name = name,
					MemberName = memberName,
					IsField = isField,
					IsReadOnly = isReadOnly,
					IsInitOnly = isInitOnly,
					IsRequired = isRequired,
					IsNotNull = isNotNull,
					IsKey = isKey,
				};
			}

		}
		
	}
	
}
