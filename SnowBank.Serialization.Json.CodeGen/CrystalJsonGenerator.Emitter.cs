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
	using System.CodeDom.Compiler;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Threading;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	[Generator(LanguageNames.CSharp)]
	public partial class CrystalJsonSourceGenerator : IIncrementalGenerator
	{

		internal const string JsonValueFullNameFullName = KnownTypeSymbols.CrystalJsonNamespace + ".JsonValue";

		internal const string CrystalJsonSettingsFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonSettings";
		
		internal const string ICrystalJsonTypeResolverFullName = KnownTypeSymbols.CrystalJsonNamespace + ".ICrystalJsonTypeResolver";
		
		internal const string JsonNullFullName = KnownTypeSymbols.CrystalJsonNamespace + ".JsonNull";

		internal const string JsonObjectFullName = KnownTypeSymbols.CrystalJsonNamespace + ".JsonObject";

		internal const string JsonArrayFullName = KnownTypeSymbols.CrystalJsonNamespace + ".JsonArray";

		internal const string JsonConverterInterfaceFullName = KnownTypeSymbols.CrystalJsonNamespace + ".IJsonConverter";
		
		internal const string JsonSerializerExtensionsFullName = KnownTypeSymbols.CrystalJsonNamespace + ".JsonSerializerExtensions";

		internal const string CrystalJsonGeneratedNamespace = "CrystalJsonGenerated";

		internal const string CrystalJsonGeneratedNamespaceGlobal = "global::" + CrystalJsonGeneratedNamespace;

		internal const string CrystalJsonConverterAttributePrefix = "CrystalJsonConverter";

		internal const string CrystalJsonConverterAttributeTypeName = CrystalJsonConverterAttributePrefix + "Attribute";

		internal const string CrystalJsonConverterAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonConverterAttribute";
		
		internal const string CrystalJsonSerializableAttributePrefix = "CrystalJsonSerializable";

		internal const string CrystalJsonSerializableAttributeTypeName = CrystalJsonSourceGenerator.CrystalJsonSerializableAttributePrefix + "Attribute";

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

			var knownTypeSymbols = context.CompilationProvider.Select((compilation, _) => new KnownTypeSymbols(compilation));
			
			// find all possible converters (partial classes with a [CrystalJsonConverter] attribute)
			var converterTypes = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    CrystalJsonConverterAttributeFullName,
                    (node, _) => node is ClassDeclarationSyntax,
                    (context, _) => (ContextClass: (ClassDeclarationSyntax) context.TargetNode, context.SemanticModel, context.Attributes)
                )
                .Combine(knownTypeSymbols)
                .Select(static (tuple, ct) =>
                {
                    var parser = new Parser(tuple.Right);
                    var contextGenerationSpec = parser.ParseContainerMetadata(tuple.Left.ContextClass, tuple.Left.SemanticModel, tuple.Left.Attributes, ct);
                    var diagnostics = ImmutableEquatableArray<DiagnosticInfo>.Empty; //TODO!
                    return (Metadata: contextGenerationSpec, Diagnostics: diagnostics, Symbols: tuple.Right);
                })
                .WithTrackingName("CrystalJsonSpec")
				;

			context.RegisterSourceOutput(converterTypes, EmitSourceCode);
		}

		private void EmitSourceCode(SourceProductionContext ctx, (CrystalJsonContainerMetadata? Metadata, ImmutableEquatableArray<DiagnosticInfo> Diagnostics, KnownTypeSymbols Symbols) args)
		{
			try
			{
				foreach (DiagnosticInfo diagnostic in args.Diagnostics)
				{
					ctx.ReportDiagnostic(diagnostic.CreateDiagnostic());
				}

				if (args.Metadata is not null)
				{
					var emitter = new Emitter(ctx, args.Symbols);
					emitter.GenerateCode(args.Metadata);
				}
			}
			catch (Exception ex)
			{
				Kenobi("CRASH: " + ex.ToString());
			}
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

		internal sealed class Emitter
		{
			
			private SourceProductionContext Context { get; }
			
			private KnownTypeSymbols KnownSymbols { get; }

			public Emitter(SourceProductionContext ctx, KnownTypeSymbols knownSymbols)
			{
				this.Context = ctx;
				this.KnownSymbols = knownSymbols;
			}
			
			public void GenerateCode(CrystalJsonContainerMetadata obj)
			{
				this.Context.CancellationToken.ThrowIfCancellationRequested();

				Kenobi($"GEN [{DateTime.UtcNow:hh:mm:ss}]: {obj.Symbol.Name}");

				var code = GenerateConverterSource(obj);
			
				Kenobi("Use the source:\r\n" + code);
			
				this.Context.AddSource($"{obj.Symbol.Name}.g.cs", code);
			}

			public string GenerateConverterSource(CrystalJsonContainerMetadata metadata)
			{
				Kenobi($"Generated {metadata.Symbol.Name} with {metadata.IncludedTypes.Count} included types");

				var sb = new CodeBuilder();
				sb.Comment("<auto-generated/>");
				sb.NewLine();

				sb.Comment($"Name: '{metadata.Symbol.Name}'");
				sb.Comment($"NameSpace: '{metadata.Symbol.NameSpace}'");
				sb.Comment($"Types: {metadata.IncludedTypes.Count}");
				foreach (var t in metadata.IncludedTypes)
				{
					sb.Comment($"- {t.Name}");
				}

				//sb.AppendLine("#if NET8_0_OR_GREATER");
				//sb.NewLine();
				sb.AppendLine("#nullable enable annotations");
				sb.AppendLine("#nullable enable warnings"); //TODO: REVIEW: to disable or not to disable warnings?
				sb.NewLine();

				sb.Namespace(
					metadata.Symbol.NameSpace,
					() =>
					{
						// we don't want to have to specify the namespace everytime
						sb.AppendLine($"using {KnownTypeSymbols.CrystalJsonNamespace};");
						// we also use a lot of helper static methods from this type
						sb.AppendLine($"using static {JsonSerializerExtensionsFullName};");
						sb.NewLine();

						sb.AppendLine("/// <summary>Generated source code for JSON operations on application types</summary>");
						//sb.Attribute<DynamicallyAccessedMembersAttribute>([sb.Constant(DynamicallyAccessedMemberTypes.All)]);
						sb.Attribute<GeneratedCodeAttribute>([sb.Constant(nameof(CrystalJsonSourceGenerator)), sb.Constant("0.1")]);
						sb.Attribute<DebuggerNonUserCodeAttribute>();
						sb.AppendLine($"public static partial class {metadata.Symbol.Name}");
						sb.EnterBlock("Container");
						foreach (var typeDef in metadata.IncludedTypes)
						{
							GenerateCodeForType(sb, metadata, typeDef);
						}
						sb.LeaveBlock("Container");
						sb.NewLine();
					}
				);

				sb.NewLine();
				//sb.AppendLine("#endif");

				Kenobi("Done!");

				return sb.ToStringAndClear();
			}

			public void GenerateCodeForType(CodeBuilder sb, CrystalJsonContainerMetadata metadata, CrystalJsonTypeMetadata typeDef)
			{
				var typeFullName = typeDef.Symbol.FullyQualifiedName;
				var typeName = typeDef.Symbol.Name;

				var serializerName = typeName;
				var serializerTypeName = serializerName + "JsonConverter";
			
				sb.Comment($"Generating for type {typeDef.Symbol.FullyQualifiedName}");
				foreach (var member in typeDef.Members)
				{
					sb.Comment($"- {member.Name}: {member.Type.Name} {member.MemberName}");
				}

				sb.AppendLine($"/// <summary>JSON converter for type <see cref=\"{typeFullName}\">{typeName}</see></summary>");
				sb.AppendLine($"public static {serializerTypeName} {serializerName} => m_cached{serializerName} ??= new();");
				sb.NewLine();
				sb.AppendLine($"private static {serializerTypeName}? m_cached{serializerName};");
				sb.NewLine();
			
				sb.AppendLine($"public sealed class {serializerTypeName} : {JsonConverterInterfaceFullName}<{typeFullName}>"); //TODO: implements!
				sb.EnterBlock("type:" + typeDef.Name);

				// Serialize
			
				sb.AppendLine($"public void Serialize(CrystalJsonWriter writer, {typeFullName}? instance)");
				sb.EnterBlock("serialize");
				//sb.AppendLine("throw new NotImplementedException();");
				sb.LeaveBlock("serialize");
				sb.NewLine();

				// Pack

				sb.AppendLine($"public {JsonValueFullNameFullName} Pack({typeFullName}? instance, {CrystalJsonSettingsFullName}? settings = default, {ICrystalJsonTypeResolverFullName}? resolver = default)");
				sb.AppendLine("{ throw new NotImplementedException(); }");
				sb.NewLine();
			
				// UnPack

				sb.AppendLine($"public {typeFullName} Unpack({CrystalJsonSourceGenerator.JsonValueFullNameFullName} value, {ICrystalJsonTypeResolverFullName}? resolver = default)");
				sb.AppendLine("{ throw new NotImplementedException(); }");
				sb.NewLine();
			
				sb.LeaveBlock("type:" + typeDef.Name);
			}
			
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

	}
	
}
