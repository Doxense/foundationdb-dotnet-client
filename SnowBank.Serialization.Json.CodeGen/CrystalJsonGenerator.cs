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

//#define LAUNCH_DEBUGGER

//#define FULL_DEBUG

namespace Doxense.Serialization.Json.CodeGen
{
	using System;
	using System.Diagnostics;
	using Microsoft.CodeAnalysis;
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

		internal const string CrystalJsonConverterAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonConverterAttribute";
		
		internal const string CrystalJsonSerializableAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonSerializableAttribute";

#if FULL_DEBUG
#pragma warning disable RS1035
		private static readonly string ProcessIdentifier = "[" + Process.GetCurrentProcess().ProcessName + ":" + Process.GetCurrentProcess().Id.ToString() + "]";
#pragma warning restore RS1035
#endif
		
		[Conditional("FULL_DEBUG")]
		public static void Kenobi(string msg)
		{
#if FULL_DEBUG
			System.Diagnostics.Debug.WriteLine(msg);
#pragma warning disable RS1035
			Console.WriteLine(msg);
#pragma warning restore RS1035
#pragma warning disable RS1035
			System.IO.File.AppendAllText(@"c:\temp\analyzer.log", $"{ProcessIdentifier} [{DateTime.Now:O}] {msg}\r\n");
#pragma warning restore RS1035
#endif
		}

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
#if LAUNCH_DEBUGGER
            System.Diagnostics.Debugger.Launch();
#endif			
		
			Kenobi("------- INITIALIZE -------------");

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
                    return (Metadata: contextGenerationSpec, Diagnostics: diagnostics);
                })
                .WithTrackingName("CrystalJsonSpec")
				;

			context.RegisterSourceOutput(converterTypes, EmitSourceCode);
		}

		private void EmitSourceCode(SourceProductionContext ctx, (CrystalJsonContainerMetadata? Metadata, ImmutableEquatableArray<DiagnosticInfo> Diagnostics) args)
		{
			try
			{
				foreach (DiagnosticInfo diagnostic in args.Diagnostics)
				{
					ctx.ReportDiagnostic(diagnostic.CreateDiagnostic());
				}

				if (args.Metadata is not null)
				{
					var emitter = new Emitter(ctx, args.Metadata);
					emitter.GenerateCode();
				}
			}
			catch (Exception ex)
			{
				Kenobi("CRASH: " + ex.ToString());
			}
		}

	}
	
}
