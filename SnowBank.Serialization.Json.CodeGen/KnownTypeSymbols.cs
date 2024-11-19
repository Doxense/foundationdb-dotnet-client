
namespace Doxense.Serialization.Json.CodeGen
{
	using Microsoft.CodeAnalysis;

	internal sealed class KnownTypeSymbols
	{

		public const string CrystalJsonNamespace = "Doxense.Serialization.Json";
		
		public Compilation Compilation { get; }

		#region Json Serialization Attributes

		public INamedTypeSymbol? CrystalJsonConverterAttribute { get; }
		
		#endregion
		
		#region JsonValue types...
		
		public INamedTypeSymbol? JsonValue { get; }
		public INamedTypeSymbol? JsonObject { get; }
		public INamedTypeSymbol? JsonArray { get; }
		public INamedTypeSymbol? JsonNull { get; }
		public INamedTypeSymbol? JsonBoolean { get; }
		public INamedTypeSymbol? JsonNumber { get; }
		public INamedTypeSymbol? JsonString { get; }
		
		#endregion
		
		public KnownTypeSymbols(Compilation compilation)
		{
			this.Compilation = compilation;

			this.CrystalJsonConverterAttribute = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".CrystalJsonConverterAttribute");

			this.JsonValue = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".JsonValue");
			this.JsonObject = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".JsonObject");
			this.JsonArray = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".JsonArray");
			this.JsonNull = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".JsonNull");
			this.JsonBoolean = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".JsonBoolean");
			this.JsonNumber = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".JsonNumber");
			this.JsonString = compilation.GetBestTypeByMetadataName(CrystalJsonNamespace + ".JsonString");
		}
		
	}
	
}
