using System;

namespace Doxense.Serialization.Json.CodeGen
{
	using Microsoft.CodeAnalysis;

	internal sealed class KnownTypeSymbols
	{

		public Compilation Compilation { get; }

		public INamedTypeSymbol? JsonValue { get; }
		
		public KnownTypeSymbols(Compilation compilation)
		{
			this.Compilation = compilation;

			this.JsonValue = compilation.GetBestTypeByMetadataName("Microsoft.Extensions.Configuration.BinderOptions");
		}
		
	}
}
