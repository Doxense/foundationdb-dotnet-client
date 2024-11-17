#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#define FULL_DEBUG

namespace Doxense.Serialization.Json.CodeGen
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	[Generator(LanguageNames.CSharp)]
	public class CrystalJsonSourceGenerator : IIncrementalGenerator
	{

		internal const string CrystalJsonGeneratedNamespace = "CrystalJsonGenerated";
		internal const string CrystalJsonGeneratedNamespaceGlobal = "global::" + CrystalJsonGeneratedNamespace;

		internal const string CrystalJsonConverterAttributePrefix = "CrystalJsonConverter";
		internal const string CrystalJsonConverterAttributeTypeName = CrystalJsonConverterAttributePrefix + "Attribute";
		internal const string CrystalJsonConverterAttributeNamespace = "Doxense.Serialization.Json";

		internal const string CrystalJsonIncludeAttributePrefix = "CrystalJsonInclude";
		internal const string CrystalJsonIncludeAttributeTypeName = CrystalJsonIncludeAttributePrefix + "Attribute";

		[Conditional("FULL_DEBUG")]
		public static void Kenobi(string msg)
		{
#if FULL_DEBUG
			System.Diagnostics.Debug.WriteLine(msg);
#pragma warning disable RS1035
			Console.WriteLine(msg);
#pragma warning restore RS1035
#pragma warning disable RS1035
			System.IO.File.AppendAllText(@"c:\temp\analyzer.log", $"[{DateTime.Now:O}] {msg}\r\n");
#pragma warning restore RS1035
#endif
		}

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			Kenobi("------- INITIALIZE -------------");

			//REVIEW: do we need a generated global file ?
			//context.RegisterPostInitializationOutput((pi) =>
			//{
			//	pi.AddSource(
			//		"CrystalJson_MainAttributes__.g.cs",
			//		$"""
			//		 namespace {CrystalJsonGeneratedNamespace};

			//		 //TODO: put global code here!
			//		 """
			//	);
			//});

			// find all possible converters (partial classes with a [CrystalJsonConverter] attribute)
			var converterTypes = context.SyntaxProvider
				.CreateSyntaxProvider(CouldBeConverterAttribute, GetGeneratedTypeMetadata)
				.Where(type => type is not null)
				.WithComparer(CrystalJsonTypeMetadataComparer.Default)
				.Collect()
				.SelectMany((types, _) => types.Distinct())
				;

			context.RegisterSourceOutput(converterTypes, (ctx, node) =>
			{
				if (node != null) GenerateCode(ctx, node);
			});
		}

		private static string? ExtractName(NameSyntax? name)
		{
			return name switch
			{
				SimpleNameSyntax ins => ins.Identifier.Text,
				QualifiedNameSyntax qns => qns.Right.Identifier.Text,
				_ => null
			};
		}

		/// <summary>Quickly test if a type matches a container for generated json code</summary>
		/// <remarks>C</remarks>
		private static bool CouldBeConverterAttribute(
			SyntaxNode syntaxNode,
			CancellationToken cancellationToken)
		{
			// must be an attribute with at least one argument
			if (syntaxNode is not AttributeSyntax attrib || (attrib.ArgumentList?.Arguments.Count ?? 0) != 0) return false;

			var name = ExtractName(attrib.Name);
			if (name == null || (name != CrystalJsonConverterAttributeTypeName && name != CrystalJsonConverterAttributePrefix))
			{
				return false;
			}

			return true;
		}

		private static CrystalJsonTypeMetadata? GetGeneratedTypeMetadata(
			GeneratorSyntaxContext context,
			CancellationToken cancellationToken)
		{
			var attributeSyntax = (AttributeSyntax) context.Node;

			// "attribute.Parent" is "AttributeListSyntax"
			// "attribute.Parent.Parent" is a C# fragment the attributes are applied to
			if (attributeSyntax.Parent?.Parent is not ClassDeclarationSyntax classDeclaration)
				return null;

			if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not ITypeSymbol type) return null;
			var attr = GetConverterAttribute(type);
			if (attr == null) return null;

			var name = type.Name;

			Kenobi($"Let's go ! {name}");
			return new CrystalJsonTypeMetadata(type, name, attr);
		}

		private static AttributeData? GetConverterAttribute(ITypeSymbol type)
		{
			Kenobi("Checking: " + type.Name);

			foreach (var attribute in type.GetAttributes())
			{
				Kenobi($"- {attribute.AttributeClass?.Name}, {attribute.AttributeClass?.ContainingNamespace.ToDisplayString()}");
				var attributeClass = attribute.AttributeClass;
				if (attributeClass == null) continue;
				if (attributeClass.Name != CrystalJsonConverterAttributeTypeName) continue;
				if (attributeClass.ContainingNamespace.ToDisplayString() != CrystalJsonConverterAttributeNamespace) continue;
				return attribute;
			}

			return null;
		}

		private static void GenerateCode(SourceProductionContext ctx, CrystalJsonTypeMetadata obj)
		{
			ctx.CancellationToken.ThrowIfCancellationRequested();

			Kenobi($"GEN [{DateTime.UtcNow:hh:mm:ss}]: {obj.Symbol.Name}");

			var code = GenerateConverterSource(obj);
			var typeNamespace = obj.Symbol.ContainingNamespace.IsGlobalNamespace
				? null
				: $"{obj.Symbol.ContainingNamespace}.";

			ctx.AddSource($"{typeNamespace}{obj.Symbol.Name}.g.cs", code);
		}

		static string GetGlobalTypeName(ITypeSymbol type)
		{
			switch (type.SpecialType)
			{
				case SpecialType.None:
				{
					if (type is IArrayTypeSymbol arr && arr.ElementType.SpecialType != SpecialType.None)
					{
						return type.ToDisplayString();
					}
					if (type.ContainingNamespace.Name == "System") return type.ToDisplayString();
					return "global::" + type.ToDisplayString();
				}
				default: return type.ToDisplayString();
			}
		}

		static string GenerateConverterSource(CrystalJsonTypeMetadata obj)
		{

			Kenobi($"Generated {obj.Name}");

			var type = obj.Symbol;
			var sb = new StringBuilder();

			sb.AppendLine($$"""
				// <auto-generated/>

				#nullable enable

				namespace {{type.ContainingNamespace.ToDisplayString()}};

				/**
				    Name: {{obj.Name}}
				*/

				""");

			sb.AppendLine($"public static partial class {type.Name}");
			sb.AppendLine("{");
			sb.AppendLine();

			foreach (var t in obj.IncludedTypes)
			{
				Kenobi($"output: {t.Name}");
				sb.AppendLine($"    // Type: {t.Name}");
				sb.AppendLine($"    public static class {t.Name}Converter");
				sb.AppendLine("    {");
				sb.AppendLine($"        public static string Serialize(object instance) => \"\\\"hello, there!\\\"\";");
				sb.AppendLine("    }");
				sb.AppendLine();
			}

			sb.AppendLine("}");

			Kenobi("Done!");

			return sb.ToString();
		}

	}

	public sealed record CrystalJsonTypeMetadata
	{
		public string Name { get; }

		public ITypeSymbol Symbol { get; }

		public AttributeData? Attribute { get; }

		public List<INamedTypeSymbol> IncludedTypes { get; }

		public CrystalJsonTypeMetadata(ITypeSymbol symbol, string name, AttributeData attr)
		{
			this.Name = name;
			this.Symbol = symbol;
			this.Attribute = attr;

			var includedTypes = new List<INamedTypeSymbol>();
			foreach (var x in symbol.GetAttributes())
			{
				var ac = x.AttributeClass;
				if (ac == null) continue;

				CrystalJsonSourceGenerator.Kenobi("check " + ac.Name);
				if (ac.Name != CrystalJsonSourceGenerator.CrystalJsonIncludeAttributeTypeName && ac.Name != CrystalJsonSourceGenerator.CrystalJsonIncludeAttributePrefix) continue;
				CrystalJsonSourceGenerator.Kenobi("check type args " + x.ConstructorArguments.Length);
				if (x.ConstructorArguments.Length < 1) continue;
				CrystalJsonSourceGenerator.Kenobi($"check arg0 {x.ConstructorArguments[0].Type?.ToDisplayString()} {x.ConstructorArguments[0].Value?.GetType().FullName}");
				if (x.ConstructorArguments[0].Value is not INamedTypeSymbol t) continue;
				CrystalJsonSourceGenerator.Kenobi($"Include type {t}");
				includedTypes.Add(t);
			}
			this.IncludedTypes = includedTypes;
		}

		public bool Equals(CrystalJsonTypeMetadata? other)
		{
			return CrystalJsonTypeMetadataComparer.Default.Equals(this, other);
		}

		public override int GetHashCode()
		{
			return CrystalJsonTypeMetadataComparer.Default.GetHashCode(this);
		}

	}

	internal sealed class CrystalJsonTypeMetadataComparer : IEqualityComparer<CrystalJsonTypeMetadata?>
	{

		public static readonly CrystalJsonTypeMetadataComparer Default = new();

		private CrystalJsonTypeMetadataComparer() { }

		public bool Equals(CrystalJsonTypeMetadata? x, CrystalJsonTypeMetadata? y)
		{
			if (ReferenceEquals(x, y)) return true;
			if (x is null || y is null) return false;

			if (x.Name != y.Name) return false;
			if (x.IncludedTypes.Count != y.IncludedTypes.Count) return false;
			for (int i = 0; i < x.IncludedTypes.Count; i++)
			{
				if (!SymbolEqualityComparer.Default.Equals(x.IncludedTypes[i], y.IncludedTypes[i])) return false;
			}

			return true;
		}

		public int GetHashCode(CrystalJsonTypeMetadata? obj)
		{
			if (obj == null) return -1;
			var hashCode = obj.Name.GetHashCode();
			//TODO: more ?
			return hashCode;
		}

	}

}
