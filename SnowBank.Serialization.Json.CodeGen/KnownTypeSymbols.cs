
namespace SnowBank.Serialization.Json.CodeGen
{
	using Microsoft.CodeAnalysis;

	[SuppressMessage("ReSharper", "InconsistentNaming")]
	internal sealed class KnownTypeSymbols
	{

		#region FullNames constants...

		public const string CrystalJsonNamespace = "Doxense.Serialization.Json";

		public const string CrystalJsonFullName = CrystalJsonNamespace + ".CrystalJson";
		public const string CrystalJsonConverterAttributeFullName = CrystalJsonNamespace + ".CrystalJsonConverterAttribute";
		public const string JsonPropertyAttributeFullName = CrystalJsonNamespace + ".JsonPropertyAttribute";

		public const string JsonEncodedPropertyNameFullName = CrystalJsonNamespace + ".JsonEncodedPropertyName";
		public const string CrystalJsonSettingsFullName = CrystalJsonNamespace + ".CrystalJsonSettings";
		public const string CrystalJsonVisitorFullName = CrystalJsonNamespace + ".CrystalJsonVisitor";
		public const string CrystalJsonWriterFullName = CrystalJsonNamespace + ".CrystalJsonWriter";
		public const string CrystalJsonMarshallFullName = CrystalJsonNamespace + ".CrystalJsonMarshall";
		public const string JsonSerializerExtensionsFullName = CrystalJsonNamespace + ".JsonSerializerExtensions";

		public const string ICrystalJsonTypeResolverFullName = CrystalJsonNamespace + ".ICrystalJsonTypeResolver";
		public const string IJsonSerializableFullName = CrystalJsonNamespace + ".IJsonSerializable";
		public const string IJsonPackableFullName = CrystalJsonNamespace + ".IJsonPackable";
		public const string IJsonReadOnlyProxyFullName = CrystalJsonNamespace + ".IJsonReadOnlyProxy";
		public const string IJsonMutableProxyFullName = CrystalJsonNamespace + ".IJsonMutableProxy";
		public const string IJsonConverterInterfaceFullName = CrystalJsonNamespace + ".IJsonConverter";
		public const string IJsonMutableParentFullName = CrystalJsonNamespace + ".IJsonMutableParent";

		public const string JsonValueFullName = CrystalJsonNamespace + ".JsonValue";
		public const string JsonNullFullName = CrystalJsonNamespace + ".JsonNull";
		public const string JsonObjectFullName = CrystalJsonNamespace + ".JsonObject";
		public const string JsonArrayFullName = CrystalJsonNamespace + ".JsonArray";
		public const string JsonNumberFullName = CrystalJsonNamespace + ".JsonNumber";
		public const string JsonBooleanFullName = CrystalJsonNamespace + ".JsonBoolean";
		public const string JsonStringFullName = CrystalJsonNamespace + ".JsonString";
		public const string JsonDateTimeFullName = CrystalJsonNamespace + ".JsonDateTime";

		public const string JsonMutableProxyObjectBaseFullName = CrystalJsonNamespace + ".JsonMutableProxyObjectBase";
		public const string JsonReadOnlyProxyObjectFullName = CrystalJsonNamespace + ".JsonReadOnlyProxyObject";
		public const string JsonReadOnlyProxyArrayFullName = CrystalJsonNamespace + ".JsonReadOnlyProxyArray";
		public const string JsonMutableProxyDictionaryFullName = CrystalJsonNamespace + ".JsonMutableProxyDictionary";
		public const string JsonMutableProxyArrayFullName = CrystalJsonNamespace + ".JsonMutableProxyArray";

		#endregion
		
		public Compilation Compilation { get; }

		#region JSON Serialization Attributes...

		public INamedTypeSymbol? CrystalJsonConverterAttribute { get; }
		
		#endregion

		#region JSON Serilization Interfaces...
		public INamedTypeSymbol? IJsonSerializable { get; }

		public INamedTypeSymbol? IJsonPackable { get; }

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

			this.CrystalJsonConverterAttribute = compilation.GetBestTypeByMetadataName(CrystalJsonConverterAttributeFullName);

			this.IJsonPackable = compilation.GetBestTypeByMetadataName(IJsonPackableFullName);
			this.IJsonSerializable = compilation.GetBestTypeByMetadataName(IJsonSerializableFullName);

			this.JsonValue = compilation.GetBestTypeByMetadataName(JsonValueFullName);
			this.JsonObject = compilation.GetBestTypeByMetadataName(JsonObjectFullName);
			this.JsonArray = compilation.GetBestTypeByMetadataName(JsonArrayFullName);
			this.JsonNull = compilation.GetBestTypeByMetadataName(JsonNullFullName);
			this.JsonBoolean = compilation.GetBestTypeByMetadataName(JsonBooleanFullName);
			this.JsonNumber = compilation.GetBestTypeByMetadataName(JsonNumberFullName);
			this.JsonString = compilation.GetBestTypeByMetadataName(JsonStringFullName);
		}
		
	}
	
}
