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

namespace SnowBank.Serialization.Json.CodeGen
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

		/// <summary>Parses the symbols from the compilation, in order to extract metadata for the serialization of application types</summary>
		internal sealed class Parser
		{

			private const string RequiredMemberAttributeFullName = "System.Runtime.CompilerServices.RequiredMemberAttribute";

			private const string KeyAttributeFullName = "System.ComponentModel.DataAnnotations.KeyAttribute";

			/// <summary>Table of known symbols from this compilation</summary>
			private KnownTypeSymbols KnownSymbols { get; }

			public List<DiagnosticInfo> Diagnostics { get; } = [ ];
			
			private Location? ContextClassLocation { get; set; }

			public Parser(KnownTypeSymbols knownSymbols)
			{
				this.KnownSymbols = knownSymbols;
			}

			public void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
			{
				Debug.Assert(this.ContextClassLocation != null);

				if (location is null || !ContainsLocation(this.KnownSymbols.Compilation, location))
				{
					// If location is null or is a location outside the current compilation, fall back to the location of the context class.
					location = this.ContextClassLocation;
				}

				this.Diagnostics.Add(DiagnosticInfo.Create(descriptor, location, messageArgs));

				static bool ContainsLocation(Compilation compilation, Location location)
					=> location.SourceTree != null && compilation.ContainsSyntaxTree(location.SourceTree);

			}

			public CrystalJsonContainerMetadata? ParseContainerMetadata(ClassDeclarationSyntax contextClassDeclaration, SemanticModel semanticModel, ImmutableArray<AttributeData> attributes, CancellationToken cancellationToken)
			{
				// we are inspecting the "container" type that will host all the generated serialization code
				// - the container should be a partial class
				// - it should have the [CrystalJsonConverter] attribute applied to it (which is the marker for triggering this code generator)
				// - it should have one or more [CrystalJsonSerializable(typeof(...))] attributes for each of the "root" application types to serialize
				
				var symbol = semanticModel.GetDeclaredSymbol(contextClassDeclaration, cancellationToken);
				if (symbol == null) return null;

				this.ContextClassLocation = contextClassDeclaration.GetLocation();
				
				Kenobi($"ParseContainerMetadata({symbol.Name}, [{attributes.Length}])");

				var langVersion = (this.KnownSymbols.Compilation as CSharpCompilation)?.LanguageVersion;
				if (langVersion is null or < LanguageVersion.CSharp9)
				{
					// Unsupported lang version should be the first (and only) diagnostic emitted by the generator.
					ReportDiagnostic(
						new(
							"CJSON0003",
							"You must include at least one type in {0} by adding one ore more [CrystalJsonSerializable] attributes",
							"The project target C# language version {0} which is lower than the minimum supported version {1}.",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						langVersion?.ToDisplayString(), LanguageVersion.CSharp9.ToDisplayString());
					return null;
				}

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
						ReportDiagnostic(
							new DiagnosticDescriptor(
								"CJSON0001",
								"Failed to parse JSON metadata",
								"Failed to extract the JSON serialization metadata for type {0}: {1}",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Error,
								isEnabledByDefault: true
							),
							this.ContextClassLocation,
							type.ToDisplayString(),
							ex.ToString()
						);
					}
				}

				if (includedTypes.Count == 0)
				{
					ReportDiagnostic(
						new(
							"CJSON0002",
							"At least one type must be included",
							"The container type {0} must specify at only one application type to include, using the [CrystalJsonSerializable] attribute",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Warning,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						symbol.ToDisplayString()
					);
				}

				var containerName = symbol.Name;

				this.ContextClassLocation = null;

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

						var setMethod = property.SetMethod;
						isInitOnly = false;
						if (setMethod is not null)
						{
							foreach (var mod in setMethod.ReturnTypeCustomModifiers)
							{
								if (mod.Modifier.Name == "IsExternalInit")
								{
									isInitOnly = true;
								}
							}
						}

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
						isInitOnly = false; // not possible on a field
						break;
					}
					default:
					{
						return null;
					}
				}

				var type = TypeMetadata.Create(typeSymbol);

				var attributes = typeSymbol.GetAttributes().Select(attr => attr.ToString()).ToImmutableEquatableArray();

				bool isNotNull;
				if (type.IsValueType())
				{ 
					// only Nullable<T> is nullable
					isNotNull = type.NullableOfType == null;
				}
				else
				{
					// look for nullability annotations
					//TODO: should we check if nullability annotations are enabled for ths type/assembly?
					isNotNull = type.Nullability == NullableAnnotation.NotAnnotated;
				}

				// parameters that can be modified via attributes or keywords on the member
				var name = memberName;
				bool isRequired = false;
				bool isKey = false;
				string? defaultValueLiteral = null;
				
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
							//TODO: check if a default value was provided!
							break;
						}
						case KeyAttributeFullName:
						{
							isKey = true;
							break;
						}
						case RequiredMemberAttributeFullName:
						{
							isRequired = true;
							break;
						}
						//TODO: any other argument for setting a default value?
					}
				}
				
				return new()
				{
					Type = type,
					Name = name,
					MemberName = memberName,
					Attributes = attributes,
					IsField = isField,
					IsReadOnly = isReadOnly,
					IsInitOnly = isInitOnly,
					IsRequired = isRequired,
					IsNotNull = isNotNull,
					IsKey = isKey,
					DefaultValueLiteral = defaultValueLiteral,
				};
			}

		}
		
	}
	
}
